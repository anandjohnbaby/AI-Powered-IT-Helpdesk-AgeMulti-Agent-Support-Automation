using Helpdesk.Api.Extensions;
using Helpdesk.Application.Agent;
using Helpdesk.Application.Common;
using Helpdesk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = AuthenticationExtensions.EmployeeOnly)] // IT approvers cannot use the chat
public class ChatController(HelpdeskAgent agent, IAppDbContext db, ICurrentUser currentUser, ApprovalService approvals)
    : ControllerBase
{
    public record ChatRequest(string Message);

    /// <summary>Send a message to the helpdesk agent.</summary>
    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatReply>> Send(ChatRequest request, CancellationToken ct)
        => Ok(await agent.ChatAsync(request.Message, ct));

    /// <summary>Your chat history (including approval notifications).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var name = currentUser.Username;
        var messages = await db.ChatMessages.Where(m => m.Username == name)
            .OrderBy(m => m.Id).Select(m => new { m.Role, m.Content, m.CreatedAt }).ToListAsync(ct);
        return Ok(messages);
    }

    /// <summary>Your approval requests and their status.</summary>
    [HttpGet("my-requests")]
    public async Task<IActionResult> MyRequests(CancellationToken ct)
        => Ok(await approvals.ListForUserAsync(currentUser.Username, ct));
}
