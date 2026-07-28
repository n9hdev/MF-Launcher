using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "superadmin")]
public class RulesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISignatureEngine _engine;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public RulesController(AppDbContext db, ISignatureEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var entities = await _db.EngineRules
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return Ok(entities.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var entity = await _db.EngineRules.FindAsync(new[] { id }, ct);
        if (entity == null) return NotFound();
        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EngineRuleDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.MatchType))
            return BadRequest("Name and MatchType are required");

        var entity = new EngineRuleEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            Description = dto.Description ?? "",
            Severity = dto.Severity ?? "medium",
            Category = dto.Category ?? "",
            MatchType = dto.MatchType,
            ConditionsJson = dto.Conditions != null ? JsonSerializer.Serialize(dto.Conditions, JsonOpts) : null,
            PatternsJson = JsonSerializer.Serialize(dto.Patterns ?? new List<string>(), JsonOpts),
            TagsJson = JsonSerializer.Serialize(dto.Tags ?? new List<string>(), JsonOpts),
            Enabled = dto.Enabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.EngineRules.Add(entity);
        await _db.SaveChangesAsync(ct);
        ReloadEngine();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] EngineRuleDto dto, CancellationToken ct)
    {
        var entity = await _db.EngineRules.FindAsync(new[] { id }, ct);
        if (entity == null) return NotFound();

        entity.Name = dto.Name ?? entity.Name;
        entity.Description = dto.Description ?? entity.Description;
        entity.Severity = dto.Severity ?? entity.Severity;
        entity.Category = dto.Category ?? entity.Category;
        entity.MatchType = dto.MatchType ?? entity.MatchType;
        entity.ConditionsJson = dto.Conditions != null ? JsonSerializer.Serialize(dto.Conditions, JsonOpts) : entity.ConditionsJson;
        entity.PatternsJson = JsonSerializer.Serialize(dto.Patterns ?? new List<string>(), JsonOpts);
        entity.TagsJson = JsonSerializer.Serialize(dto.Tags ?? new List<string>(), JsonOpts);
        entity.Enabled = dto.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        ReloadEngine();

        return Ok(ToDto(entity));
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(string id, CancellationToken ct)
    {
        var entity = await _db.EngineRules.FindAsync(new[] { id }, ct);
        if (entity == null) return NotFound();

        entity.Enabled = !entity.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        ReloadEngine();

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var entity = await _db.EngineRules.FindAsync(new[] { id }, ct);
        if (entity == null) return NotFound();

        _db.EngineRules.Remove(entity);
        await _db.SaveChangesAsync(ct);
        ReloadEngine();

        return NoContent();
    }

    private void ReloadEngine()
    {
        var dbRules = _db.EngineRules.Where(r => r.Enabled).ToList();
        var models = dbRules.Select(e => new SignatureRuleModel
        {
            Name = e.Name,
            Description = e.Description,
            Severity = e.Severity,
            Category = e.Category,
            MatchType = e.MatchType,
            Conditions = !string.IsNullOrEmpty(e.ConditionsJson)
                ? JsonSerializer.Deserialize<RuleConditions>(e.ConditionsJson, JsonOpts)
                : null,
            Patterns = !string.IsNullOrEmpty(e.PatternsJson)
                ? JsonSerializer.Deserialize<List<string>>(e.PatternsJson, JsonOpts) ?? new()
                : new(),
            Tags = !string.IsNullOrEmpty(e.TagsJson)
                ? JsonSerializer.Deserialize<List<string>>(e.TagsJson, JsonOpts) ?? new()
                : new(),
        }).ToList();

        _engine.ReloadRules(models);
    }

    private static EngineRuleDto ToDto(EngineRuleEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        Severity = e.Severity,
        Category = e.Category,
        MatchType = e.MatchType,
        Conditions = !string.IsNullOrEmpty(e.ConditionsJson)
            ? JsonSerializer.Deserialize<RuleConditions>(e.ConditionsJson, JsonOpts)
            : null,
        Patterns = !string.IsNullOrEmpty(e.PatternsJson)
            ? JsonSerializer.Deserialize<List<string>>(e.PatternsJson, JsonOpts) ?? new()
            : new(),
        Tags = !string.IsNullOrEmpty(e.TagsJson)
            ? JsonSerializer.Deserialize<List<string>>(e.TagsJson, JsonOpts) ?? new()
            : new(),
        Enabled = e.Enabled,
        HitCount = e.HitCount,
        LastMatchTime = e.LastMatchTime,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
