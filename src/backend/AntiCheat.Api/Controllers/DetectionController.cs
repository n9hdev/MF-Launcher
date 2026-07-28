using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "moderator,admin,superadmin")]
public class DetectionController : ControllerBase
{
    private readonly IDetectionEngine _engine;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly IRiskEngine _riskEngine;
    private readonly IEvidenceCollector _evidenceCollector;
    private readonly IRuleManagerService _ruleManager;

    public DetectionController(
        IDetectionEngine engine,
        IConfidenceScorer confidenceScorer,
        IRiskEngine riskEngine,
        IEvidenceCollector evidenceCollector,
        IRuleManagerService ruleManager)
    {
        _engine = engine;
        _confidenceScorer = confidenceScorer;
        _riskEngine = riskEngine;
        _evidenceCollector = evidenceCollector;
        _ruleManager = ruleManager;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _engine.GetStatusAsync();
        return Ok(status);
    }

    [HttpPost("scan")]
    public async Task<IActionResult> RunScan(CancellationToken ct)
    {
        var results = await _engine.RunFullScanAsync(ct);
        return Ok(results);
    }

    [HttpPost("prelaunch-scan")]
    public async Task<IActionResult> RunPreLaunchScan(CancellationToken ct)
    {
        var results = await _engine.RunPreLaunchScanAsync(ct);
        return Ok(new
        {
            Clean = results.Count == 0,
            Events = results,
            Message = results.Count == 0
                ? "System clean — game launch is allowed."
                : "Threats detected — game launch blocked.",
        });
    }

    [HttpGet("detectors")]
    public IActionResult GetDetectors()
    {
        var dets = _engine.Detectors.Select(d => new
        {
            d.Name,
            d.Version,
            d.IsEnabled,
        });
        return Ok(dets);
    }

    [HttpPost("detectors/{name}/toggle")]
    [Authorize]
    public async Task<IActionResult> ToggleDetector(string name, [FromBody] ToggleRequest request)
    {
        if (request.Enabled)
            await _engine.EnableDetector(name);
        else
            await _engine.DisableDetector(name);
        return Ok(new { message = $"Detector '{name}' {(request.Enabled ? "enabled" : "disabled")}" });
    }

    [HttpPost("detectors/{name}/enable")]
    [Authorize]
    public async Task<IActionResult> EnableDetector(string name)
    {
        await _engine.EnableDetector(name);
        return Ok(new { message = $"Detector '{name}' enabled" });
    }

    [HttpPost("detectors/{name}/disable")]
    [Authorize]
    public async Task<IActionResult> DisableDetector(string name)
    {
        await _engine.DisableDetector(name);
        return Ok(new { message = $"Detector '{name}' disabled" });
    }

    [HttpPost("assess")]
    public async Task<IActionResult> AssessRisk([FromBody] List<DetectionEventDto> events, CancellationToken ct)
    {
        var assessment = await _riskEngine.AssessRiskAsync(events, ct);
        return Ok(assessment);
    }

    [HttpGet("confidence")]
    public IActionResult CalculateConfidence([FromQuery] double rawConfidence, [FromQuery] int detectionCount = 1, [FromQuery] int totalProcesses = 1)
    {
        var score = _confidenceScorer.CalculateConfidence(rawConfidence, detectionCount, totalProcesses);
        var severity = _confidenceScorer.ClassifySeverity(score);
        return Ok(new { confidence = score, severity });
    }

    [HttpPost("evidence/capture")]
    public async Task<IActionResult> CaptureEvidence([FromBody] EvidenceRequest request, CancellationToken ct)
    {
        var ev = new DetectionEventDto
        {
            Id = request.EventId,
            Type = request.EventType,
            Severity = request.Severity,
        };
        var evidence = await _evidenceCollector.CaptureProcessSnapshotAsync(request.ProcessId, ev, ct);
        return Ok(evidence);
    }

    [HttpGet("evidence/{eventId}")]
    public async Task<IActionResult> GetEvidence(string eventId)
    {
        var evidence = await _evidenceCollector.GetEvidenceAsync(eventId);
        return Ok(evidence);
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        var rules = await _ruleManager.GetRulesAsync();
        return Ok(rules);
    }

    [HttpGet("rules/{id}")]
    public async Task<IActionResult> GetRule(string id)
    {
        var rule = await _ruleManager.GetRuleAsync(id);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    [HttpPost("rules")]
    public async Task<IActionResult> AddRule([FromBody] DetectionRuleDto rule)
    {
        var created = await _ruleManager.AddRuleAsync(rule);
        return CreatedAtAction(nameof(GetRule), new { id = created.Id }, created);
    }

    [HttpPut("rules/{id}")]
    public async Task<IActionResult> UpdateRule(string id, [FromBody] DetectionRuleDto rule)
    {
        rule.Id = id;
        var updated = await _ruleManager.UpdateRuleAsync(rule);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("rules/{id}")]
    public async Task<IActionResult> DeleteRule(string id)
    {
        var deleted = await _ruleManager.DeleteRuleAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("rules/{id}/toggle")]
    public async Task<IActionResult> ToggleRule(string id, [FromBody] ToggleRequest request)
    {
        var toggled = await _ruleManager.ToggleRuleAsync(id, request.Enabled);
        if (!toggled) return NotFound();
        return Ok(new { message = $"Rule '{id}' toggled to {request.Enabled}" });
    }
}

public class EvidenceRequest
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public int ProcessId { get; set; }
}

public class ToggleRequest
{
    public bool Enabled { get; set; }
}
