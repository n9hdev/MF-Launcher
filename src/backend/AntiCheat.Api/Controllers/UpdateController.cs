using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AntiCheat.Core.Configuration;

namespace AntiCheat.Api.Controllers;

[ApiController]
[Route("api/updates")]
public class UpdateController : ControllerBase
{
    private readonly UpdateOptions _updateOptions;

    public UpdateController(IOptions<UpdateOptions> updateOptions)
    {
        _updateOptions = updateOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet("latest")]
    public IActionResult GetLatest()
    {
        return Ok(new
        {
            version = _updateOptions.LatestVersion,
            releaseDate = _updateOptions.ReleaseDate,
            downloadUrl = _updateOptions.DownloadUrl,
            fallbackDownloadUrl = _updateOptions.FallbackDownloadUrl,
            sha256 = _updateOptions.Sha256,
            size = _updateOptions.Size,
            changelog = _updateOptions.Changelog,
            isCritical = _updateOptions.IsCritical,
            minSupportedVersion = _updateOptions.MinSupportedVersion,
            signature = _updateOptions.Signature,
        });
    }
}
