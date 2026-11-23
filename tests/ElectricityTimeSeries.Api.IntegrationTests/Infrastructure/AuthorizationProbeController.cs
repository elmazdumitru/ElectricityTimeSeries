using ElectricityTimeSeries.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>Test-only endpoints for checking the real authorization policies. Added only in <see cref="ApiFactory"/>.</summary>
[ApiController]
[Route("test/authorization")]
public class AuthorizationProbeController : ControllerBase
{
    [HttpGet("read")]
    [Authorize(Policy = Policies.CanReadTimeSeries)]
    public IActionResult Read() => Ok();

    [HttpPost("write")]
    [Authorize(Policy = Policies.CanWriteTimeSeries)]
    public IActionResult Write() => Ok();

    /// <summary>No [Authorize] attribute: protected only by the fallback policy.</summary>
    [HttpGet("no-policy")]
    public IActionResult NoPolicy() => Ok();
}
