using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/reputation")]
public class ReputationController : ControllerBase
{
    private readonly IReputationService _reputation;

    public ReputationController(IReputationService reputation)
    {
        _reputation = reputation;
    }

    [HttpPost("lookup")]
    public async Task<ActionResult<ReputationLookupResponse>> Lookup([FromBody] ReputationLookupRequest request, CancellationToken ct)
    {
        var result = await _reputation.LookupAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("report")]
    [Authorize]
    public async Task<ActionResult<ReputationLookupResponse>> Report([FromBody] ReputationReportRequest request, CancellationToken ct)
    {
        var result = await _reputation.ReportAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("verdict")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<ActionResult<ReputationLookupResponse>> SetVerdict([FromBody] ReputationVerdictRequest request, CancellationToken ct)
    {
        var result = await _reputation.SetVerdictAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<ActionResult<object>> GetCount(CancellationToken ct)
    {
        var count = await _reputation.GetTotalEntriesAsync(ct);
        return Ok(new { totalEntries = count });
    }
}
