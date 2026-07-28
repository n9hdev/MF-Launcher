using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Core.Services;

public class SandboxService : ISandboxService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SandboxService> _logger;
    private readonly SandboxSettings _settings;
    private readonly ConcurrentDictionary<string, SandboxResultEntity> _cache = new();

    public bool IsAvailable => _settings.Enabled && OperatingSystem.IsWindows();

    public SandboxService(
        IServiceScopeFactory scopeFactory,
        IOptions<SandboxSettings> settings,
        ILogger<SandboxService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<SandboxResult> AnalyseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || !File.Exists(filePath))
        {
            return new SandboxResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                Verdict = "skipped",
                Description = "Sandbox not available or file not found",
            };
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _settings.MaxFileSizeMb * 1024L * 1024L)
        {
            return new SandboxResult
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                Verdict = "skipped",
                Description = $"File exceeds max analysis size ({_settings.MaxFileSizeMb}MB)",
            };
        }

        var sha256 = await ComputeSha256Async(filePath, cancellationToken);

        var cached = await GetCachedResultAsync(sha256, cancellationToken);
        if (cached != null)
            return cached;

        var sbDir = PrepareSandboxDirectory(sha256);
        var tempCopy = Path.Combine(sbDir, Path.GetFileName(filePath));

        Process? process = null;

        try
        {
            File.Copy(filePath, tempCopy, overwrite: true);
            var details = new ConcurrentBag<SandboxDetail>();
            var timeout = TimeSpan.FromSeconds(_settings.AnalysisTimeoutSeconds);
            var sw = Stopwatch.StartNew();

            // File system watcher
            using var fsWatcher = new FileSystemWatcher(sbDir)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            fsWatcher.Created += (_, e) =>
            {
                details.Add(new SandboxDetail
                {
                    Type = "FileWrite",
                    Value = e.Name,
                    Severity = (e.Name.EndsWith(".exe") || e.Name.EndsWith(".dll") || e.Name.EndsWith(".scr")) ? "suspicious" : "info"
                });
            };
            fsWatcher.Changed += (_, e) =>
            {
                if ((e.Name.EndsWith(".exe") || e.Name.EndsWith(".dll")) && e.ChangeType == WatcherChangeTypes.Changed)
                    details.Add(new SandboxDetail { Type = "FileModified", Value = e.Name, Severity = "suspicious" });
            };

            // Baseline snapshots
            var baselinePids = Process.GetProcesses().Select(p => p.Id).ToHashSet();
            var baselineServices = GetServiceNames();

            // Start target process
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = tempCopy,
                    WorkingDirectory = sbDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            var pid = process.Id;

            // Monitoring loop
            while (sw.Elapsed < timeout && !process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);

                // Check for new processes
                var currentProcs = Process.GetProcesses();
                foreach (var p in currentProcs)
                {
                    if (!baselinePids.Contains(p.Id))
                    {
                        baselinePids.Add(p.Id);
                        try
                        {
                            var parentPid = GetParentPid(p.Id);
                            if (parentPid == pid || parentPid == 0)
                            {
                                var isDangerous = p.ProcessName.Contains("cmd") || p.ProcessName.Contains("powershell")
                                    || p.ProcessName.Contains("wscript") || p.ProcessName.Contains("cscript");
                                details.Add(new SandboxDetail
                                {
                                    Type = "ProcessCreated",
                                    Value = $"{p.ProcessName} (PID: {p.Id})",
                                    Severity = isDangerous ? "high" : "medium",
                                });
                            }
                        }
                        catch { /* access denied */ }
                    }
                }

                // Poll network connections
                foreach (var conn in GetNetworkConnectionsForPid(pid))
                {
                    details.Add(new SandboxDetail { Type = "NetworkConnection", Value = conn, Severity = "medium" });
                }

                // Poll new services
                foreach (var svc in GetServiceNames())
                {
                    if (!baselineServices.Contains(svc))
                    {
                        baselineServices.Add(svc);
                        details.Add(new SandboxDetail { Type = "ServiceCreated", Value = svc, Severity = "high" });
                    }
                }
            }

            sw.Stop();
            KillProcessTree(pid);

            // Collect module info for the original pid
            int dllCount = 0;
            try
            {
                using var proc = Process.GetProcessById(pid);
                dllCount = proc.Modules.Count;
            }
            catch { }

            var detailList = details.ToList();
            var result = BuildResult(sha256, filePath, fileInfo.Name, detailList, dllCount);
            await PersistResult(sha256, result, detailList, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sandbox analysis failed for {File}", filePath);
            return new SandboxResult
            {
                Sha256 = sha256,
                FileName = fileInfo.Name,
                FilePath = filePath,
                Verdict = "error",
                Description = $"Sandbox analysis error: {ex.Message}",
            };
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                process.Dispose();
            }
            await CleanupSandbox(sbDir);
        }
    }

    public async Task<SandboxResult?> GetCachedResultAsync(string sha256, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(sha256, out var cached))
            return MapFromCached(cached);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.SandboxResults.FirstOrDefaultAsync(r => r.Sha256 == sha256, cancellationToken);
            if (entity != null)
            {
                _cache.TryAdd(sha256, entity);
                return MapFromCached(entity);
            }
        }
        catch { }

        return null;
    }

    // ─── Monitoring helpers ─────────────────────────────────────

    private static int GetParentPid(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (var obj in searcher.Get())
                return Convert.ToInt32(obj["ParentProcessId"]);
        }
        catch { }
        return 0;
    }

    private static List<string> GetNetworkConnectionsForPid(int pid)
    {
        var results = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT RemoteAddress, RemotePort, ConnectionState FROM MsTcp_ActiveConnection WHERE ProcessId = {pid}");
            foreach (var obj in searcher.Get())
            {
                var addr = obj["RemoteAddress"]?.ToString();
                var port = obj["RemotePort"]?.ToString();
                var state = obj["ConnectionState"]?.ToString() ?? "unknown";
                if (!string.IsNullOrEmpty(addr) && addr != "0.0.0.0")
                    results.Add($"{addr}:{port} ({state})");
            }
        }
        catch { }

        try
        {
            using var searcher2 = new ManagementObjectSearcher(
                $"SELECT RemoteAddress, RemotePort, State FROM Win32_NetworkConnection WHERE ProcessId = {pid}");
            foreach (var obj in searcher2.Get())
            {
                var addr = obj["RemoteAddress"]?.ToString();
                var port = obj["RemotePort"]?.ToString();
                var state = obj["State"]?.ToString() ?? "unknown";
                if (!string.IsNullOrEmpty(addr) && addr != "0.0.0.0")
                    results.Add($"{addr}:{port} ({state})");
            }
        }
        catch { }

        return results;
    }

    private static HashSet<string> GetServiceNames()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Service");
            return new HashSet<string>(searcher.Get()
                .Cast<ManagementObject>()
                .Select(o => o["Name"]?.ToString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(); }
    }

    private static void KillProcessTree(int pid)
    {
        try
        {
            using var killer = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /F /T",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            killer.Start();
            killer.WaitForExit(3000);
        }
        catch { }

        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill(entireProcessTree: true);
        }
        catch { }
    }

    // ─── Result building ─────────────────────────────────────────

    private static SandboxResult BuildResult(string sha256, string filePath, string fileName,
        List<SandboxDetail> details, int dllCount)
    {
        int procCount = details.Count(d => d.Type == "ProcessCreated");
        int fileWriteCount = details.Count(d => d.Type == "FileWrite" || d.Type == "FileModified");
        int netCount = details.Count(d => d.Type == "NetworkConnection");
        int svcCount = details.Count(d => d.Type == "ServiceCreated");
        bool suspiciousProc = details.Any(d => d.Type == "ProcessCreated" && d.Severity == "high");
        bool wroteExe = details.Any(d => d.Type == "FileWrite" && d.Severity == "suspicious");

        double score = ComputeSuspicionScore(details);
        var (verdict, severity, desc) = Classify(suspiciousProc, wroteExe, netCount > 0, score);

        return new SandboxResult
        {
            Sha256 = sha256,
            FileName = fileName,
            FilePath = filePath,
            Verdict = verdict,
            SuspicionScore = Math.Round(score, 4),
            Severity = severity,
            Description = desc,
            ProcessCount = procCount,
            FileWriteCount = fileWriteCount,
            NetworkConnectionCount = netCount,
            ServiceCount = svcCount,
            DllLoadCount = dllCount,
            CreatedSuspiciousProcess = suspiciousProc,
            WroteExecutableFile = wroteExe,
            ConnectedToNetwork = netCount > 0,
            Details = details,
        };
    }

    private static double ComputeSuspicionScore(List<SandboxDetail> details)
    {
        double score = 0;
        foreach (var d in details)
        {
            score += d.Severity switch
            {
                "critical" => 0.3,
                "high" => 0.2,
                "suspicious" => 0.15,
                "medium" => 0.1,
                _ => 0.02,
            };
        }
        return Math.Min(score, 1.0);
    }

    private static (string verdict, string severity, string desc) Classify(
        bool suspiciousProc, bool wroteExe, bool connected, double score)
    {
        if (suspiciousProc || score >= 0.7)
            return ("malicious", "critical", $"Suspicious process creation detected (score: {score:F2})");
        if (wroteExe || score >= 0.4)
            return ("suspicious", "high", $"Executable file written by sample (score: {score:F2})");
        if (connected || score >= 0.2)
            return ("suspicious", "medium", $"Network activity detected (score: {score:F2})");
        if (score > 0)
            return ("low_risk", "info", $"Minimal activity detected (score: {score:F2})");
        return ("benign", "info", "No suspicious behavior observed during analysis window");
    }

    private static SandboxResult MapFromCached(SandboxResultEntity e)
    {
        var details = string.IsNullOrWhiteSpace(e.DetailsJson)
            ? new List<SandboxDetail>()
            : JsonSerializer.Deserialize<List<SandboxDetail>>(e.DetailsJson) ?? new();

        return new SandboxResult
        {
            Sha256 = e.Sha256,
            Verdict = e.Verdict,
            SuspicionScore = e.SuspicionScore,
            Severity = e.SuspicionScore >= 0.7 ? "critical" : e.SuspicionScore >= 0.4 ? "high" : e.SuspicionScore >= 0.2 ? "medium" : "info",
            Description = e.SuspicionScore >= 0.4 ? "Suspicious behavior detected (cached)" : "No threats detected (cached)",
            ProcessCount = e.ProcessCount,
            FileWriteCount = e.FileWriteCount,
            NetworkConnectionCount = e.NetworkConnectionCount,
            ServiceCount = e.ServiceCount,
            CreatedSuspiciousProcess = e.CreatedSuspiciousProcess,
            WroteExecutableFile = e.WroteExecutableFile,
            ConnectedToNetwork = e.ConnectedToNetwork,
            Details = details,
        };
    }

    private async Task PersistResult(string sha256, SandboxResult result,
        List<SandboxDetail> details, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SandboxResults.Add(new SandboxResultEntity
            {
                Sha256 = sha256,
                Verdict = result.Verdict,
                ProcessCount = result.ProcessCount,
                FileWriteCount = result.FileWriteCount,
                NetworkConnectionCount = result.NetworkConnectionCount,
                ServiceCount = result.ServiceCount,
                DllLoadCount = result.DllLoadCount,
                CreatedSuspiciousProcess = result.CreatedSuspiciousProcess,
                WroteExecutableFile = result.WroteExecutableFile,
                ConnectedToNetwork = result.ConnectedToNetwork,
                ModifiedRegistry = false,
                SuspicionScore = result.SuspicionScore,
                DetailsJson = JsonSerializer.Serialize(details),
                AnalysedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist sandbox result for {Sha256}", sha256);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private string PrepareSandboxDirectory(string sha256)
    {
        var dir = Path.Combine(Path.GetTempPath(), _settings.TempDirectory, sha256[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async Task CleanupSandbox(string path)
    {
        if (!_settings.CleanupOnExit) return;
        try
        {
            await Task.Run(() =>
            {
                try { Directory.Delete(path, recursive: true); }
                catch { }
            });
        }
        catch { }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fs, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }
}
