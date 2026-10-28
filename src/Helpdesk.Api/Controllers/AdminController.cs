using Helpdesk.Api.Extensions;
using Helpdesk.Application.Common;
using Helpdesk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthenticationExtensions.ApproverOnly)]
public class AdminController(ICurrentUser currentUser, ApprovalService approvals, DirectoryAdminService directory,
    IAppDbContext db, IServerService servers) : ControllerBase
{
    public record DecisionRequest(bool Approve, string? Note);
    public record DirectoryActionRequest(string? Reason);

    /// <summary>All approval requests.</summary>
    [HttpGet("approvals")]
    public async Task<IActionResult> Approvals(CancellationToken ct)
        => Ok(await approvals.ListAsync(ct));

    /// <summary>Approve or reject a pending request. Rejection requires a note.</summary>
    [HttpPost("approvals/{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request, CancellationToken ct)
        => Ok(await approvals.DecideAsync(id, currentUser.Username, request.Approve, request.Note, ct));

    /// <summary>Directory (Keycloak) accounts with lock status.</summary>
    [HttpGet("directory-users")]
    public async Task<IActionResult> DirectoryUsers(CancellationToken ct)
        => Ok(await directory.ListAsync(ct));

    /// <summary>Unlock a directory account. Reason required (audited).</summary>
    [HttpPost("directory-users/{username}/unlock")]
    public async Task<IActionResult> Unlock(string username, DirectoryActionRequest request, CancellationToken ct)
        => Ok(new { message = await directory.UnlockAsync(username, request.Reason, ct) });

    /// <summary>Reset a directory password (temporary password emailed). Reason required (audited).</summary>
    [HttpPost("directory-users/{username}/reset-password")]
    public async Task<IActionResult> ResetPassword(string username, DirectoryActionRequest request, CancellationToken ct)
        => Ok(new { message = await directory.ResetPasswordAsync(username, request.Reason, ct) });

    /// <summary>Latest 100 audit entries.</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken ct)
        => Ok(await db.AuditLogs.OrderByDescending(a => a.Id).Take(100).ToListAsync(ct));

    /// <summary>Managed servers (Docker containers with the helpdesk label).</summary>
    [HttpGet("servers")]
    public async Task<IActionResult> Servers(CancellationToken ct)
        => Ok(await servers.ListAsync(ct));
}
