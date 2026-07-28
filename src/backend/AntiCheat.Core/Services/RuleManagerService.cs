using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class RuleManagerService : IRuleManagerService
{
    private readonly ILogger<RuleManagerService> _logger;
    private static readonly List<DetectionRuleDto> _rules = new();
    private static readonly ReaderWriterLockSlim _rulesLock = new();
    private static bool _seeded;

    public RuleManagerService(ILogger<RuleManagerService> logger)
    {
        _logger = logger;
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        if (_seeded) return;
        _seeded = true;

        _rules.AddRange(new[]
        {
            new DetectionRuleDto { Id = "dr1", Name = "RWX Memory Region", Description = "Detects executable writeable memory pages", DetectorType = "Memory Scanner", Pattern = "RWX", Severity = "high", ConfidenceThreshold = 0.5 },
            new DetectionRuleDto { Id = "dr2", Name = "Known Cheat Process", Description = "Detects known cheat engine processes", DetectorType = "Process Analyzer", Pattern = "cheatengine|artmoney", Severity = "critical", ConfidenceThreshold = 0.3 },
            new DetectionRuleDto { Id = "dr3", Name = "Suspicious DLL Load", Description = "Detects known injected DLLs", DetectorType = "Injection Detector", Pattern = "xinput|d3d9", Severity = "high", ConfidenceThreshold = 0.5 },
            new DetectionRuleDto { Id = "dr4", Name = "Unsigned Driver", Description = "Detects unsigned kernel drivers", DetectorType = "Kernel Scanner", Pattern = "unsigned", Severity = "medium", ConfidenceThreshold = 0.4 },
            new DetectionRuleDto { Id = "dr5", Name = "Debugger API Call", Description = "Detects debugger detection API calls", DetectorType = "YARA Scanner", Pattern = "IsDebuggerPresent", Severity = "medium", ConfidenceThreshold = 0.6 },
        });
    }

    public Task<List<DetectionRuleDto>> GetRulesAsync()
    {
        _rulesLock.EnterReadLock();
        try
        {
            return Task.FromResult(_rules.ToList());
        }
        finally
        {
            _rulesLock.ExitReadLock();
        }
    }

    public Task<DetectionRuleDto?> GetRuleAsync(string id)
    {
        _rulesLock.EnterReadLock();
        try
        {
            return Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));
        }
        finally
        {
            _rulesLock.ExitReadLock();
        }
    }

    public Task<DetectionRuleDto> AddRuleAsync(DetectionRuleDto rule)
    {
        rule.Id = Guid.NewGuid().ToString();
        _rulesLock.EnterWriteLock();
        try
        {
            _rules.Add(rule);
        }
        finally
        {
            _rulesLock.ExitWriteLock();
        }
        _logger.LogInformation("Rule added: {Name} ({Id})", rule.Name, rule.Id);
        return Task.FromResult(rule);
    }

    public Task<DetectionRuleDto?> UpdateRuleAsync(DetectionRuleDto rule)
    {
        _rulesLock.EnterWriteLock();
        try
        {
            var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing == null) return Task.FromResult<DetectionRuleDto?>(null);

            existing.Name = rule.Name;
            existing.Description = rule.Description;
            existing.DetectorType = rule.DetectorType;
            existing.Pattern = rule.Pattern;
            existing.Severity = rule.Severity;
            existing.ConfidenceThreshold = rule.ConfidenceThreshold;
            existing.Enabled = rule.Enabled;
            return Task.FromResult<DetectionRuleDto?>(existing);
        }
        finally
        {
            _rulesLock.ExitWriteLock();
        }
    }

    public Task<bool> DeleteRuleAsync(string id)
    {
        _rulesLock.EnterWriteLock();
        try
        {
            var removed = _rules.RemoveAll(r => r.Id == id);
            if (removed > 0)
                _logger.LogInformation("Rule deleted: {Id}", id);
            return Task.FromResult(removed > 0);
        }
        finally
        {
            _rulesLock.ExitWriteLock();
        }
    }

    public Task<bool> ToggleRuleAsync(string id, bool enabled)
    {
        _rulesLock.EnterWriteLock();
        try
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return Task.FromResult(false);

            rule.Enabled = enabled;
            _logger.LogInformation("Rule {Id} toggled to {Enabled}", id, enabled);
            return Task.FromResult(true);
        }
        finally
        {
            _rulesLock.ExitWriteLock();
        }
    }

    public Task<List<DetectionRuleDto>> GetRulesForDetectorAsync(string detectorType)
    {
        _rulesLock.EnterReadLock();
        try
        {
            var results = _rules.Where(r =>
                r.DetectorType.Equals(detectorType, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(results);
        }
        finally
        {
            _rulesLock.ExitReadLock();
        }
    }
}
