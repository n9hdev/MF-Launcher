using System.Text.RegularExpressions;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class SignatureEngineService : ISignatureEngine
{
    private List<SignatureRuleModel> _rules;
    private readonly ILogger<SignatureEngineService> _logger;
    private static readonly HashSet<string> LuaDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "lua5.1.dll", "lua5.2.dll", "lua5.3.dll", "lua51.dll", "lua52.dll", "lua53.dll",
        "luajit.dll", "luajit-5.1.dll", "luac.dll", "lua.dll", "lua54.dll", "lua5.4.dll"
    };
    private static readonly HashSet<string> InjectionApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "OpenProcess", "WriteProcessMemory", "CreateRemoteThread", "CreateRemoteThreadEx",
        "VirtualAllocEx", "VirtualProtectEx", "QueueUserAPC", "SetWindowsHookEx",
        "NtCreateThreadEx", "NtUnmapViewOfSection"
    };
    private static readonly string[] CheatPdbKeywords = { "cheat", "hack", "crack", "inject", "bypass", "exploit", "trainer", "dumper", "crackme", "keygen", "detour", "hook" };
    private static readonly string[] MtaPdbKeywords = { "mta", "gta", "san andreas", "samp", "multi theft" };
    private static readonly string[] GameFilePrefixes = { "gta", "mta", "gtasa", "gta_sa", "samp", "multi theft" };
    private static readonly string[] SuspiciousSectionNames = { "cheat", "hack", "crack", "loader", "inject", "dll", "evil", "bad", "hook", "pwn" };

    public SignatureEngineService(ILogger<SignatureEngineService>? logger = null)
    {
        _logger = logger ?? LoggerFactory.Create(b => { }).CreateLogger<SignatureEngineService>();
        _rules = new List<SignatureRuleModel>();

        var embeddedRules = SignatureRuleLoader.LoadFromAssembly(_logger);
        if (embeddedRules.Count > 0)
        {
            _rules = embeddedRules;
            _logger.LogInformation("SignatureEngineService initialized with {Count} rules from embedded resources", _rules.Count);
            return;
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var rulesDir = Path.Combine(baseDir, "Rules");

        if (!Directory.Exists(rulesDir))
        {
            rulesDir = Path.Combine(Directory.GetCurrentDirectory(), "Rules");
        }

        if (!Directory.Exists(rulesDir))
        {
            var tryDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "AntiCheat.Core", "Rules"));
            if (Directory.Exists(tryDir))
                rulesDir = tryDir;
        }

        var fileRules = SignatureRuleLoader.LoadFromDirectory(rulesDir, _logger);
        _rules = fileRules;
        _logger.LogInformation("SignatureEngineService initialized with {Count} rules from {Dir}", _rules.Count, rulesDir);
    }

    public void ReloadRules(IEnumerable<SignatureRuleModel> rules)
    {
        _rules = rules.ToList();
        _logger.LogInformation("SignatureEngineService reloaded with {Count} rules", _rules.Count);
    }

    public IReadOnlyList<SignatureMatch> MatchPe(PeAnalysisResult pe)
    {
        if (!pe.ParsingSucceeded) return Array.Empty<SignatureMatch>();
        var matches = new List<SignatureMatch>();

        foreach (var rule in _rules)
        {
            var match = MatchPeRule(rule, pe);
            if (match != null)
                matches.Add(match);
        }

        return matches;
    }

    public IReadOnlyList<SignatureMatch> MatchProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return Array.Empty<SignatureMatch>();
        var matches = new List<SignatureMatch>();

        foreach (var rule in _rules)
        {
            if (rule.MatchType != "process_name") continue;
            if (MatchProcessPattern(processName, rule.Patterns))
            {
                matches.Add(new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = rule.Description,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "process",
                    MatchedValue = processName,
                    Tags = new List<string>(rule.Tags),
                });
            }
        }

        return matches;
    }

    public IReadOnlyList<SignatureMatch> MatchFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return Array.Empty<SignatureMatch>();
        var matches = new List<SignatureMatch>();

        foreach (var rule in _rules)
        {
            if (rule.MatchType != "file_path") continue;
            if (MatchProcessPattern(filePath, rule.Patterns))
            {
                matches.Add(new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = rule.Description,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "filepath",
                    MatchedValue = Path.GetFileName(filePath),
                    Tags = new List<string>(rule.Tags),
                });
            }
        }

        return matches;
    }

    private SignatureMatch? MatchPeRule(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        try
        {
            return rule.MatchType switch
            {
                "injection_api_set" => MatchInjectionApiSet(rule, pe),
                "dangerous_import" => MatchDangerousImport(rule, pe),
                "lua_dll" => MatchLuaDll(rule, pe),
                "packed_unsigned" => MatchPackedUnsigned(rule, pe),
                "high_entropy" => MatchHighEntropy(rule, pe),
                "high_entropy_overlay" => MatchHighEntropyOverlay(rule, pe),
                "suspicious_pdb" => MatchSuspiciousPdb(rule, pe),
                "self_signed_game_file" => MatchSelfSignedGameFile(rule, pe),
                "suspicious_section_name" => MatchSuspiciousSectionName(rule, pe),
                "rwx_section" => MatchRwxSection(rule, pe),
                "low_entropy_code" => MatchLowEntropyCode(rule, pe),
                "rsrc_executable" => MatchRsrcExecutable(rule, pe),
                "tls_callbacks" => MatchTlsCallbacks(rule, pe),
                "unsigned_dll_game_dir" => MatchUnsignedDllGameDir(rule, pe),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error evaluating rule {Rule} on {File}", rule.Name, pe.FilePath);
            return null;
        }
    }

    private SignatureMatch? MatchInjectionApiSet(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var cond = rule.Conditions;
        if (cond?.Apis == null || cond.Apis.Count == 0) return null;

        var dangerous = pe.Imports.DangerousImports;
        var matchedApis = cond.Apis.Where(a => dangerous.Any(d =>
            d.ApiName.Equals(a, StringComparison.OrdinalIgnoreCase))).ToList();

        if (matchedApis.Count < (cond.MinApiCount > 0 ? cond.MinApiCount : cond.Apis.Count))
            return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (matched {matchedApis.Count}/{cond.Apis.Count}: {string.Join(", ", matchedApis)})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = string.Join(",", matchedApis),
            Tags = new List<string>(rule.Tags) { "injection" },
        };
    }

    private SignatureMatch? MatchDangerousImport(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var cond = rule.Conditions;
        if (cond?.Apis == null || cond.Apis.Count == 0) return null;

        foreach (var targetApi in cond.Apis)
        {
            var match = pe.Imports.DangerousImports.FirstOrDefault(d =>
                d.ApiName.Equals(targetApi, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = $"{rule.Description} (from {match.DllName})",
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "pe",
                    MatchedValue = $"{targetApi} in {match.DllName}",
                    Tags = new List<string>(rule.Tags) { targetApi.ToLowerInvariant() },
                };
            }
        }

        return null;
    }

    private SignatureMatch? MatchLuaDll(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var found = pe.Imports.Dlls.FirstOrDefault(d => LuaDlls.Contains(d));
        if (found == null) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (DLL: {found})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = found,
            Tags = new List<string>(rule.Tags) { "lua", "executor" },
        };
    }

    private SignatureMatch? MatchPackedUnsigned(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        if (!pe.Packer.IsPacked) return null;
        var unsigned = pe.Signature == null || !pe.Signature.IsSigned;
        if (!unsigned) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (packer: {pe.Packer.PackerName ?? "unknown"}, entropy: {pe.Entropy.FileEntropy:F2})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = pe.Packer.PackerName ?? "unknown_packer",
            Tags = new List<string>(rule.Tags) { "packed", "unsigned" },
        };
    }

    private SignatureMatch? MatchHighEntropy(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var threshold = rule.Conditions?.EntropyThreshold > 0 ? rule.Conditions.EntropyThreshold : 7.0;
        if (pe.Entropy.FileEntropy <= threshold) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (entropy: {pe.Entropy.FileEntropy:F2})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = $"{pe.Entropy.FileEntropy:F2}",
            Tags = new List<string>(rule.Tags) { "high_entropy" },
        };
    }

    private SignatureMatch? MatchHighEntropyOverlay(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        if (!pe.Overlay.Exists) return null;
        var threshold = rule.Conditions?.EntropyThreshold > 0 ? rule.Conditions.EntropyThreshold : 7.0;
        if (pe.Overlay.Entropy <= threshold) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (overlay entropy: {pe.Overlay.Entropy:F2}, size: {pe.Overlay.Size})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = $"entropy={pe.Overlay.Entropy:F2},size={pe.Overlay.Size}",
            Tags = new List<string>(rule.Tags) { "overlay", "payload" },
        };
    }

    private SignatureMatch? MatchSuspiciousPdb(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var pdb = pe.Debug.PdbPath;
        if (string.IsNullOrWhiteSpace(pdb)) return null;
        var lowerPdb = pdb.ToLowerInvariant();

        foreach (var kw in CheatPdbKeywords)
        {
            if (lowerPdb.Contains(kw))
            {
                return new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = $"{rule.Description} (PDB: {pdb}, keyword: {kw})",
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "pe",
                    MatchedValue = pdb,
                    Tags = new List<string>(rule.Tags) { "pdb", kw },
                };
            }
        }

        foreach (var kw in MtaPdbKeywords)
        {
            if (lowerPdb.Contains(kw))
            {
                return new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = $"{rule.Description} (PDB: {pdb}, keyword: {kw})",
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "pe",
                    MatchedValue = pdb,
                    Tags = new List<string>(rule.Tags) { "pdb", "mta" },
                };
            }
        }

        return null;
    }

    private SignatureMatch? MatchSelfSignedGameFile(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        if (pe.Signature == null || !pe.Signature.IsSelfSigned) return null;

        var fileName = Path.GetFileNameWithoutExtension(pe.FilePath).ToLowerInvariant();
        var lowerPath = pe.FilePath.ToLowerInvariant();

        foreach (var prefix in GameFilePrefixes)
        {
            if (fileName.StartsWith(prefix) || lowerPath.Contains(prefix))
            {
                return new SignatureMatch
                {
                    RuleName = rule.Name,
                    Description = $"{rule.Description} (file: {Path.GetFileName(pe.FilePath)}, subject: {pe.Signature.Subject})",
                    Category = rule.Category,
                    Severity = rule.Severity,
                    MatchType = "pe",
                    MatchedValue = pe.Signature.Subject,
                    Tags = new List<string>(rule.Tags) { "self_signed" },
                };
            }
        }

        return null;
    }

    private SignatureMatch? MatchSuspiciousSectionName(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var names = rule.Conditions?.SuspiciousSectionNames ?? SuspiciousSectionNames.ToList();
        var suspicious = pe.Sections.FirstOrDefault(s =>
            names.Any(n => s.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0));
        if (suspicious == null) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (section: {suspicious.Name})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = suspicious.Name,
            Tags = new List<string>(rule.Tags) { "section", "suspicious_name" },
        };
    }

    private SignatureMatch? MatchRwxSection(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var rwxSections = pe.Sections.Where(s => s.IsExecutable && s.IsWritable && !s.IsDiscardable).ToList();
        if (rwxSections.Count == 0) return null;

        var names = string.Join(", ", rwxSections.Select(s => s.Name));
        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} ({rwxSections.Count} section(s): {names})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = names,
            Tags = new List<string>(rule.Tags) { "section", "rwx" },
        };
    }

    private SignatureMatch? MatchLowEntropyCode(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var threshold = rule.Conditions?.CodeEntropyThreshold > 0 ? rule.Conditions.CodeEntropyThreshold : 1.0;
        var lowEntropy = pe.Sections
            .Where(s => s.IsCode || s.IsExecutable)
            .Select(s => new { s.Name, s.Entropy })
            .FirstOrDefault(s => s.Entropy < threshold);
        if (lowEntropy == null) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (section: {lowEntropy.Name}, entropy: {lowEntropy.Entropy:F2})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = $"{lowEntropy.Name}:{lowEntropy.Entropy:F2}",
            Tags = new List<string>(rule.Tags) { "section", "low_entropy" },
        };
    }

    private SignatureMatch? MatchRsrcExecutable(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        var rsrs = pe.Sections.FirstOrDefault(s =>
            s.Name.Equals(".rsrc", StringComparison.OrdinalIgnoreCase) && s.IsExecutable);
        if (rsrs == null) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = rule.Description,
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = ".rsrc executable",
            Tags = new List<string>(rule.Tags) { "section", "rsrc" },
        };
    }

    private SignatureMatch? MatchTlsCallbacks(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        if (!pe.Tls.HasTls || pe.Tls.CallbackCount == 0) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} ({pe.Tls.CallbackCount} callback(s))",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = $"{pe.Tls.CallbackCount} callbacks",
            Tags = new List<string>(rule.Tags) { "tls", "anti_debug" },
        };
    }

    private SignatureMatch? MatchUnsignedDllGameDir(SignatureRuleModel rule, PeAnalysisResult pe)
    {
        if (!pe.FilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return null;
        if (pe.Signature is { IsSigned: true }) return null;

        var lowerPath = pe.FilePath.ToLowerInvariant();
        var inGameDir = GameFilePrefixes.Any(p => lowerPath.Contains(p));
        if (!inGameDir) return null;

        return new SignatureMatch
        {
            RuleName = rule.Name,
            Description = $"{rule.Description} (DLL: {Path.GetFileName(pe.FilePath)})",
            Category = rule.Category,
            Severity = rule.Severity,
            MatchType = "pe",
            MatchedValue = Path.GetFileName(pe.FilePath),
            Tags = new List<string>(rule.Tags) { "dll", "unsigned", "sideloading" },
        };
    }

    private static bool MatchProcessPattern(string name, List<string> patterns)
    {
        var lowerName = name.ToLowerInvariant();
        foreach (var pattern in patterns)
        {
            var lowerPattern = pattern.ToLowerInvariant();
            if (lowerPattern.EndsWith("*.exe"))
            {
                if (lowerName.StartsWith(lowerPattern[..^5]))
                    return true;
            }
            else if (lowerPattern.EndsWith("*"))
            {
                if (lowerName.StartsWith(lowerPattern[..^1]))
                    return true;
            }
            else if (lowerName.Equals(lowerPattern))
            {
                return true;
            }
            else if (lowerName.Contains(lowerPattern))
            {
                return true;
            }
        }
        return false;
    }
}
