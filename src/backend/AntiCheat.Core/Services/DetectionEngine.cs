using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AntiCheat.Core.Data;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class DetectionEngine : IDetectionEngine, IDisposable
{
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    private readonly List<IDetector> _detectors;
    private readonly ILogger<DetectionEngine> _logger;
    private readonly IWhitelistProvider _whitelist;
    private readonly IVerdictService _verdictService;
    private readonly IBaselineService _baselineService;
    private readonly IDeltaMonitorService _deltaMonitor;
    private readonly IEvidenceCollector _evidenceCollector;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDedupService _dedupService;
    private readonly PropertyInfo[] _statusProperties;
    private bool _disposed;

    private BaselineSnapshot? _baseline;
    private bool _baselineCaptured;
    private readonly object _baselineLock = new();

    public IReadOnlyList<IDetector> Detectors => _detectors.AsReadOnly();
    public event EventHandler<DetectionEventDto>? OnDetection;
    public event EventHandler? OnPreLaunchComplete;

    public DetectionEngine(
        IEnumerable<IDetector> detectors,
        ILogger<DetectionEngine> logger,
        IWhitelistProvider whitelist,
        IVerdictService verdictService,
        IBaselineService baselineService,
        IDeltaMonitorService deltaMonitor,
        IEvidenceCollector evidenceCollector,
        IScreenCaptureService screenCapture,
        IServiceScopeFactory serviceScopeFactory,
        IDedupService dedupService)
    {
        _detectors = detectors.ToList();
        _logger = logger;
        _whitelist = whitelist;
        _verdictService = verdictService;
        _baselineService = baselineService;
        _deltaMonitor = deltaMonitor;
        _evidenceCollector = evidenceCollector;
        _screenCapture = screenCapture;
        _serviceScopeFactory = serviceScopeFactory;
        _dedupService = dedupService;
        _statusProperties = typeof(ProtectionStatusDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    /// <summary>
    /// Full scan: cross-process scans, cheat directory scans, game baseline (Phase 1+2), delta (Phase 3),
    /// and all registered detectors (ProcessAnalyzer, YARA, ClamAV, etc.). Deduplicates and fires events.
    /// </summary>
    /// <remarks>
    /// Lifecycle overview (orchestrated by AntiCheatHub + ScanBackgroundService):
    ///   1. Hub receives RequestPreLaunchScan from dashboard after player login
    ///   2. RunPreLaunchScanAsync()   → Phase 0: full scan, unlocks game launch if clean
    ///   3. Hub calls StartContinuousScan() on background service
    ///   4. RunContinuousScanAsync()  → external scanning loop (cross-process + cheat dirs + detectors) every 5s
    ///   5. When game process is found → RunFullScanAsync() is called for in-game detection
    ///   6. Detection events are fired via OnDetection (handled by ScanBackgroundService → DB + remote report)
    /// </remarks>
    public async Task<IReadOnlyList<DetectionEventDto>> RunFullScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultScanTimeout);
        var timeoutToken = timeoutCts.Token;

        try
        {
            var gameProcess = FindGameProcess();

            // ALWAYS scan external processes for suspicious activity (injectors, loaders, trainers, trojans, etc.)
            var crossProcessFacts = await ScanExternalProcessesAsync(gameProcess, timeoutToken);
            results.AddRange(crossProcessFacts);

            // ALWAYS scan known cheat directories for cheat files on disk
            var cheatDirFacts = await ScanCheatDirectoriesAsync(timeoutToken);
            results.AddRange(cheatDirFacts);

            // Game-specific phases (baseline + delta) only when game process is present and not whitelisted
            if (gameProcess != null)
            {
                if (await _whitelist.IsProcessWhitelistedAsync(gameProcess.ProcessName, cancellationToken))
                    goto AfterGamePhases;

                var pid = gameProcess.Id;
                var processName = gameProcess.ProcessName;

                // Phase 1: Wait for MTA initialization if not yet baselined
                if (!_baselineCaptured)
                {
                    _logger.LogInformation("Phase 1: Waiting for MTA initialization (PID {Pid})...", pid);
                    var initialized = await _baselineService.WaitForMtaInitializationAsync(pid, ct: timeoutToken);
                    if (initialized)
                    {
                        _logger.LogInformation("Phase 1 complete. MTA initialized.");
                    }
                    else
                    {
                        _logger.LogWarning("Phase 1 timeout. Proceeding with baseline anyway.");
                    }

                    // Phase 2: Comprehensive baseline scan
                    _logger.LogInformation("Phase 2: Capturing baseline for PID {Pid}...", pid);
                    var baseline = await _baselineService.CaptureBaselineAsync(pid, processName, timeoutToken);

                    lock (_baselineLock)
                    {
                        _baseline = baseline;
                        _baselineCaptured = true;
                    }

                    var baselineFacts = ConvertBaselineToEvidence(baseline);
                    results.AddRange(baselineFacts);

                    _logger.LogInformation(
                        "Phase 2 complete: {ModCount} modules, {ThreadCount} threads, {RegionCount} regions, MTA={Mta}",
                        baseline.Modules.Entries.Count, baseline.Threads.Count, baseline.Regions.Count, baseline.IsMtaInitialized);
                }

                // Phase 3: Lightweight delta scan against baseline
                BaselineSnapshot? currentBaseline;
                lock (_baselineLock)
                {
                    currentBaseline = _baseline;
                }

                if (currentBaseline != null)
                {
                    var deltaFacts = await _deltaMonitor.ScanDeltaAsync(currentBaseline, timeoutToken);
                    var deltaResults = deltaFacts.Select(EvidenceFactToEventDto).ToList();
                    results.AddRange(deltaResults);

                    if (deltaFacts.Count > 0)
                    {
                        _logger.LogInformation("Phase 3: {Count} delta observations", deltaFacts.Count);
                    }

                    var integrityFacts = await _deltaMonitor.CheckCodeIntegrityAsync(currentBaseline, timeoutToken);
                    var integrityResults = integrityFacts.Select(EvidenceFactToEventDto).ToList();
                    results.AddRange(integrityResults);

                    if (integrityFacts.Count > 0)
                    {
                        _logger.LogWarning("Phase 3: {Count} code integrity violations detected", integrityFacts.Count);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No game process found. Running external scans, cheat directory scan, and all detectors.");
            }

            AfterGamePhases:

            // ALWAYS run all registered detectors in parallel (YARA, ClamAV, CertRep, etc.)
            var detectorTasks = _detectors
                .Where(d => d.IsEnabled)
                .Select(async detector =>
                {
                    try
                    {
                        return await detector.ScanAsync(timeoutToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogTrace(ex, "Detector {Name} skipped", detector.Name);
                        return Enumerable.Empty<DetectionEventDto>();
                    }
                });
            var detectorResults = await Task.WhenAll(detectorTasks);
            foreach (var events in detectorResults)
            {
                results.AddRange(events);
            }

            // ALWAYS deduplicate to prevent re-alerting on the same detection within the window
            if (results.Count > 0)
            {
                var beforeDedup = results.Count;
                results = (await _dedupService.DeduplicateAsync(results, timeoutToken)).ToList();
                if (results.Count < beforeDedup)
                {
                    _logger.LogInformation("Dedup: suppressed {Suppressed}/{Total} duplicate events",
                        beforeDedup - results.Count, beforeDedup);
                }
            }

            // ALWAYS fire detection events — API handles verdict and auto-ban
            foreach (var ev in results)
            {
                OnDetection?.Invoke(this, ev);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Detection scan timed out after {Timeout}s", DefaultScanTimeout.TotalSeconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detection scan failed");
        }

        return results;
    }

    /// <summary>
    /// Phase 0: Fast pre-launch scan. Kills the game if already running (pre-loaded
    /// injectors), quick-checks process names and cheat directory existence, then runs
    /// only the lightweight ProcessAnalyzer detector. Deep scanning (YARA, ClamAV,
    /// CertificateRep, etc.) happens in the continuous lifecycle.
    /// Completes in under 2 seconds so the UI can unlock the game launch button
    /// without the user waiting for deep scans.
    /// </summary>
    public async Task<IReadOnlyList<DetectionEventDto>> RunPreLaunchScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectionEventDto>();

        try
        {
            // Kill game if it is already running (pre-loaded injectors / cheats)
            var gameProcess = FindGameProcess();
            if (gameProcess != null)
            {
                _logger.LogWarning("Pre-launch: game process detected (PID {Pid}). Shutting down to prevent pre-loaded cheats.", gameProcess.Id);
                gameProcess.Kill(entireProcessTree: true);
                await Task.Delay(500, cancellationToken);
                results.Add(new DetectionEventDto
                {
                    Type = "PreLaunch: Game Auto-Shutdown",
                    Severity = "warning",
                    Description = $"Game process ({gameProcess.ProcessName}, PID {gameProcess.Id}) was running during pre-launch scan. Automatically terminated.",
                    Confidence = 0.0,
                });
            }

            // FAST: Check cheat directories exist (no deep file scanning)
            foreach (var dir in GetCheatDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(dir))
                {
                    _logger.LogWarning("Pre-launch: known cheat directory found: {Dir}", dir);
                    results.Add(new DetectionEventDto
                    {
                        Type = "PreLaunch: Cheat Directory Detected",
                        Severity = "medium",
                        Description = $"Known cheat directory found: {dir}",
                        Confidence = 0.5,
                    });
                }
            }

            // FAST: Run ONLY the ProcessAnalyzer detector (name-based, no PE analysis).
            // YARA, ClamAV, TeamCymru, CertificateReputation, Memory, Injection, etc.
            // start running as soon as the continuous lifecycle fires up.
            foreach (var detector in _detectors)
            {
                if (!detector.IsEnabled) continue;
                if (detector.Name != "Process Analyzer") continue;

                try
                {
                    var events = await detector.ScanAsync(cancellationToken);
                    results.AddRange(events);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogTrace(ex, "Detector {Name} skipped during pre-launch", detector.Name);
                }
            }

            // Fire detection events — API handles verdict and auto-ban
            foreach (var ev in results)
            {
                OnDetection?.Invoke(this, ev);
            }

            _logger.LogInformation(
                results.Count == 0
                    ? "Pre-launch scan clean. System ready — game launch button can be unlocked."
                    : "Pre-launch scan complete: {Count} observations. Game launch blocked.",
                results.Count);
        }
        catch (OperationCanceledException)
        {
            OnPreLaunchComplete?.Invoke(this, EventArgs.Empty);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pre-launch scan failed");
        }

        OnPreLaunchComplete?.Invoke(this, EventArgs.Empty);

        return results;
    }

    public async Task RunContinuousScanAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("=== Continuous anti-cheat scanning started ===");

            // ===== EXTERNAL SCANNING LOOP (runs whether game is on or off) =====
            while (!ct.IsCancellationRequested)
            {
                // Always scan external processes and cheat directories
                var gameProcess = FindGameProcess();
                var externalEvents = new List<DetectionEventDto>();

                var crossEvents = await ScanExternalProcessesAsync(gameProcess, ct);
                externalEvents.AddRange(crossEvents);

                var dirEvents = await ScanCheatDirectoriesAsync(ct);
                externalEvents.AddRange(dirEvents);

                // Run all registered detectors in parallel (process name analysis, YARA, ClamAV, cert reputation, etc.)
                var detectorTasks = _detectors
                    .Where(d => d.IsEnabled)
                    .Select(async detector =>
                    {
                        try
                        {
                            return await detector.ScanAsync(ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogTrace(ex, "Detector {Name} skipped in external scan loop", detector.Name);
                            return Enumerable.Empty<DetectionEventDto>();
                        }
                    });
                var detectorResults = await Task.WhenAll(detectorTasks);
                foreach (var detEvents in detectorResults)
                {
                    externalEvents.AddRange(detEvents);
                }

                // Fire detection events — API handles verdict and auto-ban
                if (externalEvents.Count > 0)
                {
                    var deduped = (await _dedupService.DeduplicateAsync(externalEvents, ct)).ToList();
                    foreach (var ev in deduped)
                    {
                        OnDetection?.Invoke(this, ev);
                    }
                }

                // Check if game has started
                if (gameProcess != null)
                {
                    _logger.LogInformation("Game process detected (PID {Pid}). Entering in-game detection phase.", gameProcess.Id);

                    // ===== IN-GAME DETECTION PHASE =====
                    var startupEvents = await RunFullScanAsync(ct);
                    if (startupEvents.Count > 0)
                    {
                        _logger.LogInformation("In-game startup scan: {Count} observations", startupEvents.Count);
                    }

                    // Delta scan loop
                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);

                        gameProcess = FindGameProcess();
                        if (gameProcess == null)
                        {
                            _logger.LogInformation("Game process exited. Resetting baseline.");
                            lock (_baselineLock)
                            {
                                _baseline = null;
                                _baselineCaptured = false;
                            }
                            break; // Back to external scanning loop
                        }

                        var deltaEvents = await RunFullScanAsync(ct);
                        if (deltaEvents.Count > 0)
                        {
                            _logger.LogInformation("Delta scan: {Count} observations", deltaEvents.Count);
                        }
                    }

                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Continuous scanning stopped");
        }
    }



    private static List<DetectionEventDto> ConvertBaselineToEvidence(BaselineSnapshot baseline)
    {
        var results = new List<DetectionEventDto>();

        foreach (var entry in baseline.Modules.Entries)
        {
            var apisPresent = new List<string>();
            if (entry.PresentInPeb) apisPresent.Add("PEB");
            if (entry.PresentInToolhelp) apisPresent.Add("Toolhelp");
            if (entry.PresentInPsapi) apisPresent.Add("PSAPI");

            if (apisPresent.Count < 3)
            {
                results.Add(new DetectionEventDto
                {
                    Type = "Baseline: Module Enumeration Discrepancy",
                    Severity = "low",
                    Description = $"Module {entry.ModuleName} at 0x{entry.BaseAddress:X} visible in {string.Join("+", apisPresent)} but not in all APIs",
                    Confidence = 0.30,
                    ProcessName = baseline.ProcessName,
                });
            }
        }

        var privateExecCount = baseline.Regions.Count(r =>
            r.Type == 0x20000 && (r.Protect & 0xF0) != 0);

        if (privateExecCount > 0)
        {
            results.Add(new DetectionEventDto
            {
                Type = "Baseline: Executable Private Regions",
                Severity = "info",
                Description = $"Process has {privateExecCount} executable private memory regions at baseline",
                Confidence = 0.0,
                ProcessName = baseline.ProcessName,
            });
        }

        return results;
    }

    public static (double Confidence, string Severity) MapEvidenceFact(EvidenceFact fact)
    {
        if (fact.Category == "memory")
        {
            if (fact.Tags.Contains("pe_header"))
                return (0.95, "critical");
            if (fact.Tags.Contains("rwx") && fact.Tags.Contains("large_allocation"))
                return (0.80, "high");
            if (fact.IsAnomaly)
                return (0.65, "medium");
            if (fact.Tags.Contains("rwx"))
                return (0.50, "medium");
        }
        if (fact.Category == "module")
        {
            if (fact.Tags.Contains("hash_mismatch"))
                return (0.90, "critical");
            if (fact.Tags.Contains("new_module") && fact.Tags.Contains("untrusted_source"))
                return (0.85, "high");
            if (fact.Tags.Contains("new_module"))
                return (0.20, "info");
        }
        if (fact.Category == "code_integrity")
            return (0.90, "critical");
        if (fact.Category == "thread" && fact.IsAnomaly)
            return (0.70, "medium");
        if (fact.Category == "thread")
            return (0.10, "info");
        return (0.0, "info");
    }

    private static DetectionEventDto EvidenceFactToEventDto(EvidenceFact fact)
    {
        var (confidence, severity) = MapEvidenceFact(fact);
        return new DetectionEventDto
        {
            Type = $"{fact.Category}: {fact.Observation}",
            Severity = severity,
            Description = $"{fact.Interpretation}",
            Confidence = confidence,
            ProcessName = fact.ProcessName,
            Timestamp = fact.Timestamp,
        };
    }

    public Task<ProtectionStatusDto> GetStatusAsync()
    {
        var status = new ProtectionStatusDto();
        foreach (var detector in _detectors)
        {
            var propName = detector.Name.Replace(" ", "");
            for (int i = 0; i < _statusProperties.Length; i++)
            {
                if (_statusProperties[i].Name == propName)
                {
                    _statusProperties[i].SetValue(status, detector.IsEnabled ? "active" : "inactive");
                    break;
                }
            }
        }
        return Task.FromResult(status);
    }

    public Task EnableDetector(string name)
    {
        var detector = _detectors.FirstOrDefault(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (detector != null)
        {
            detector.IsEnabled = true;
            _logger.LogInformation("Enabled detector: {Name}", name);
        }
        return Task.CompletedTask;
    }

    public Task DisableDetector(string name)
    {
        var detector = _detectors.FirstOrDefault(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (detector != null)
        {
            detector.IsEnabled = false;
            _logger.LogInformation("Disabled detector: {Name}", name);
        }
        return Task.CompletedTask;
    }

    private static Process? FindGameProcess()
    {
        return Process.GetProcessesByName("gta_sa").FirstOrDefault()
            ?? Process.GetProcessesByName("MTA").FirstOrDefault();
    }

    private static readonly TimeSpan DefaultScanTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] WindowsSystemDirectories =
    {
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Windows\WinSxS",
        @"C:\Windows\Microsoft.NET",
        @"C:\Windows\assembly",
        @"C:\Windows\SystemRoot",
        @"C:\Program Files\WindowsApps",
    };

    private static bool IsWindowsSystemPath(string path)
    {
        foreach (var dir in WindowsSystemDirectories)
        {
            if (path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<List<DetectionEventDto>> ScanExternalProcessesAsync(Process? gameProcess, CancellationToken ct)
    {
        var results = new List<DetectionEventDto>();
        var gamePid = gameProcess?.Id;

        using var scope = _serviceScopeFactory.CreateScope();
        var sigEngine = scope.ServiceProvider.GetRequiredService<ISignatureEngine>();

        try
        {
            var allProcesses = Process.GetProcesses();
            var scannedCount = 0;
            const int maxProcesses = 50;

            foreach (var proc in allProcesses)
            {
                if (scannedCount >= maxProcesses) break;
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (proc.Id <= 0) continue;
                    if (gamePid.HasValue && proc.Id == gamePid.Value) continue;
                    if (proc.Id == Environment.ProcessId) continue;

                    var procName = proc.ProcessName;
                    if (await _whitelist.IsProcessWhitelistedAsync(procName, ct)) continue;

                    scannedCount++;

                    // Threat-intelligent external process scanning:
                    // Only run process NAME matching against known cheat patterns (cheat-processes.json).
                    // Full PE analysis, ClamAV, TeamCymru, and Certificate checks are reserved for
                    // cheat directory files and the game process itself — running them on every
                    // random EXE on the system produces false positives (LowEntropyCodeSection on
                    // Chrome, UnsignedModule on VS Code, etc.) which is NOT intelligence.
                    var nameMatches = sigEngine.MatchProcessName(procName);
                    foreach (var match in nameMatches)
                    {
                        results.Add(new DetectionEventDto
                        {
                            Type = $"YARA: {match.Category}",
                            Severity = match.Severity,
                            Description = $"[{match.RuleName}] {match.Description} (process: {procName})",
                            Confidence = match.Severity switch
                            {
                                "critical" => 0.90,
                                "high" => 0.80,
                                "medium" => 0.60,
                                _ => 0.40,
                            },
                            ProcessName = procName,
                        });
                    }
                }
                catch (Exception exInner)
                {
                    _logger.LogTrace(exInner, "Skipped external process {Name}", proc.ProcessName);
                }
            }

            if (scannedCount > 0)
            {
                _logger.LogInformation("Cross-process scan: {Count} processes checked, {Results} cheat-name observations",
                    scannedCount, results.Count);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cross-process scan failed");
        }

        return results;
    }

    private static string[] GetCheatDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new[]
        {
            @"A:\cheats",
            @"A:\cheats\Injector",
            @"C:\cheats",
            @"C:\cheats\Injector",
            @"D:\cheats",
            @"D:\cheats\Injector",
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(localAppData, "Temp"),
            Path.Combine(appData, "cheats"),
            Path.Combine(userProfile, "Desktop", "cheats"),
            Path.Combine(userProfile, "Desktop", "hacks"),
            Path.Combine(userProfile, "Documents", "cheats"),
        };
    }

    private async Task<List<DetectionEventDto>> ScanCheatDirectoriesAsync(CancellationToken ct)
    {
        var results = new List<DetectionEventDto>();

        using var scope = _serviceScopeFactory.CreateScope();
        var peAnalysis = scope.ServiceProvider.GetRequiredService<IPeAnalysisService>();
        var sigEngine = scope.ServiceProvider.GetRequiredService<ISignatureEngine>();
        var certRep = scope.ServiceProvider.GetRequiredService<ICertificateReputationService>();
        var clamAv = scope.ServiceProvider.GetRequiredService<IClamAvService>();
        var teamCymru = scope.ServiceProvider.GetRequiredService<ITeamCymruService>();

        try
        {
            var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in GetCheatDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                ct.ThrowIfCancellationRequested();

                try
                {
                    var files = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories));
                    foreach (var file in files)
                    {
                        if (ct.IsCancellationRequested) return results;
                        if (!foundFiles.Add(file)) continue;
                        if (foundFiles.Count > 50) break;

                        var fileName = Path.GetFileNameWithoutExtension(file);
                        _logger.LogTrace("Scanning cheat directory file: {File}", file);
                        await AnalyzeExternalFileAsync(file, fileName, peAnalysis, sigEngine, certRep, clamAv, teamCymru, results, ct);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }

            if (foundFiles.Count > 0)
            {
                _logger.LogInformation("Cheat directory scan: {Count} files analyzed, {Results} observations",
                    foundFiles.Count, results.Count);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cheat directory scan failed");
        }

        return results;
    }

    private async Task AnalyzeExternalFileAsync(
        string filePath,
        string label,
        IPeAnalysisService peAnalysis,
        ISignatureEngine sigEngine,
        ICertificateReputationService certRep,
        IClamAvService clamAv,
        ITeamCymruService teamCymru,
        List<DetectionEventDto> results,
        CancellationToken ct)
    {
        if (peAnalysis == null || sigEngine == null) return;

        var peResult = await peAnalysis.AnalyzeAsync(filePath, ct);
        if (peResult == null) return;

        var nameMatches = sigEngine.MatchProcessName(label);
        var pathMatches = sigEngine.MatchFilePath(filePath);
        var peMatches = sigEngine.MatchPe(peResult);

        foreach (var match in nameMatches.Concat(pathMatches).Concat(peMatches))
        {
            var confidence = match.Severity switch
            {
                "critical" => 0.90,
                "high" => 0.80,
                "medium" => 0.60,
                _ => 0.40,
            };
            results.Add(new DetectionEventDto
            {
                Type = $"YARA: {match.Category}",
                Severity = match.Severity,
                Description = $"[{match.RuleName}] {match.Description} (process: {label})",
                Confidence = confidence,
                EvidencePath = filePath,
                ProcessName = label,
            });
        }

        // Run external reputation services in parallel
        var externalTasks = new List<Task>();

        if (clamAv != null && clamAv.IsAvailable)
        {
            externalTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var clamResult = await clamAv.ScanFileAsync(filePath, ct);
                    if (clamResult != null && clamResult.IsInfected)
                    {
                        lock (results)
                        {
                            results.Add(new DetectionEventDto
                            {
                                Type = "ClamAV: Infected File",
                                Severity = "critical",
                                Description = $"ClamAV detected: {clamResult.VirusName} in {label} ({filePath})",
                                Confidence = 0.85,
                                EvidencePath = filePath,
                                ProcessName = label,
                            });
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogTrace(ex, "ClamAV scan failed for {File}", filePath);
                }
            }, ct));
        }

        if (teamCymru != null && teamCymru.IsAvailable && !string.IsNullOrEmpty(peResult.Hashes?.Sha256))
        {
            externalTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var tcResult = await teamCymru.LookupHashAsync(peResult.Hashes.Sha256, filePath, ct);
                    if (tcResult != null && tcResult.DetectionRate >= 0.05)
                    {
                        var tcConfidence = tcResult.DetectionRate switch
                        {
                            >= 0.50 => 0.95,
                            >= 0.20 => 0.80,
                            >= 0.05 => 0.60,
                            _ => 0.0,
                        };
                        if (tcConfidence > 0)
                        {
                            lock (results)
                            {
                                results.Add(new DetectionEventDto
                                {
                                    Type = "TeamCymru: Hash Reputation",
                                    Severity = tcResult.DetectionRate >= 0.50 ? "critical" :
                                               tcResult.DetectionRate >= 0.20 ? "high" : "medium",
                                    Description = $"TeamCymru: {tcResult.DetectionCount}/{tcResult.TotalEngines} engines detect {filePath}",
                                    Confidence = tcConfidence,
                                    EvidencePath = filePath,
                                    ProcessName = label,
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogTrace(ex, "TeamCymru lookup failed for {File}", filePath);
                }
            }, ct));
        }

        if (certRep != null)
        {
            externalTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var certEvidence = await certRep.VerifyFileCertificateAsync(filePath, ct);
                    if (certEvidence != null && certEvidence.Severity != "info")
                    {
                        var certConfidence = certEvidence.Severity switch
                        {
                            "critical" => 0.90,
                            "high" => 0.80,
                            "medium" => 0.60,
                            _ => 0.30,
                        };
                        lock (results)
                        {
                            results.Add(new DetectionEventDto
                            {
                                Type = "Certificate: " + certEvidence.EvidenceType,
                                Severity = certEvidence.Severity,
                                Description = $"{certEvidence.Description} (process: {label})",
                                Confidence = certConfidence,
                                EvidencePath = filePath,
                                ProcessName = label,
                            });
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogTrace(ex, "Certificate verification failed for {File}", filePath);
                }
            }, ct));
        }

        if (externalTasks.Count > 0)
        {
            await Task.WhenAll(externalTasks);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
