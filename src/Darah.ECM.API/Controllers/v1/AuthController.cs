using Darah.ECM.Application.Auth;
using Darah.ECM.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darah.ECM.API.Controllers.v1;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login(
        [FromBody] LoginCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<object>> Me()
    {
        var userId = User.FindFirst("uid")?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var nameAr = User.FindFirst("name_ar")?.Value;
        return Ok(ApiResponse<object>.Ok(new { userId, username, nameAr }));
    }

    [HttpPost("logout")]
    [Authorize]
    public ActionResult<ApiResponse<bool>> Logout()
        => Ok(ApiResponse<bool>.Ok(true));
}
