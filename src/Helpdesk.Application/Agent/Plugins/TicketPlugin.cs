using System.ComponentModel;
using Helpdesk.Application.Common;
using Helpdesk.Application.Services;
using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Helpdesk.Application.Agent.Plugins;

public class TicketPlugin(ICurrentUser user, IAppDbContext db, ApprovalService approvals)
{
    [KernelFunction("create_ticket")]
    [Description("Creates a support ticket for issues the agent cannot solve directly.")]
    public async Task<string> CreateTicketAsync(
        [Description("Short title")] string title,
        [Description("Detailed description")] string description, CancellationToken ct = default)
    {
        var t = new Ticket { Username = user.Username, Title = title, Description = description };
        db.Tickets.Add(t);
        await db.SaveChangesAsync(ct);
        return $"Ticket #{t.Id} created: {t.Title}";
    }

    [KernelFunction("list_my_tickets")]
    [Description("Lists the signed-in user's support tickets.")]
    public async Task<string> ListMyTicketsAsync(CancellationToken ct = default)
    {
        var name = user.Username;
        var tickets = await db.Tickets.Where(t => t.Username == name)
            .OrderByDescending(t => t.Id).Take(10).ToListAsync(ct);
        return tickets.Count == 0 ? "No tickets." : string.Join("\n", tickets.Select(t => $"#{t.Id} [{t.Status}] {t.Title}"));
    }

    [KernelFunction("list_my_approval_requests")]
    [Description("Shows the status of the signed-in user's requests waiting for or decided by IT approval.")]
    public async Task<string> ListMyApprovalRequestsAsync(CancellationToken ct = default)
    {
        var list = await approvals.ListForUserAsync(user.Username, ct);
        return list.Count == 0
            ? "No approval requests."
            : string.Join("\n", list.Select(a => $"{ApprovalService.Short(a.Id)} {a.ActionName}: {a.Status}"));
    }
}
