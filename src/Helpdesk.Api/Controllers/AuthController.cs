using Helpdesk.Application.Auth;
using Helpdesk.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Helpdesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Portal login. portal = "employee" or "it". Uses HelpBot credentials, NOT Keycloak.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));

    /// <summary>Log out: the current token is revoked on the server.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirst("jti")?.Value ?? "";
        var exp = long.TryParse(User.FindFirst("exp")?.Value, out var e)
            ? DateTimeOffset.FromUnixTimeSeconds(e).UtcDateTime
            : DateTime.UtcNow.AddHours(1);
        await auth.LogoutAsync(currentUser.Username, jti, exp, ct);
        return NoContent();
    }

    /// <summary>Who am I, according to my token.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        Username = currentUser.Username,
        Name = User.FindFirst("name")?.Value,
        Role = User.FindFirst("role")?.Value,
        Portal = User.FindFirst("portal")?.Value,
        currentUser.DirectoryAccount
    });
}