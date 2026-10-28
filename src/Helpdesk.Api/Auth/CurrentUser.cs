using System.Security.Claims;
using Helpdesk.Application.Common;

namespace Helpdesk.Api.Auth;

/// Reads the caller's identity from the VALIDATED portal JWT.
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new UnauthorizedAccessException("No HTTP context.");

    public string Username =>
        Principal.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException("Token has no subject.");

    public string? DirectoryAccount => Principal.FindFirst("dir")?.Value;

    public bool IsApprover => Principal.IsInRole(Roles.Approver);
}