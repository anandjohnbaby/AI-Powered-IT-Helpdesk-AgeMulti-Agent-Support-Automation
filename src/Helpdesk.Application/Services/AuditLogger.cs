using Helpdesk.Application.Common;
using Helpdesk.Domain;

namespace Helpdesk.Application.Services;

public class AuditLogger(IAppDbContext db)
{
    public async Task LogAsync(string actor, string action, string details, string outcome, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog { Actor = actor, Action = action, Details = details, Outcome = outcome });
        await db.SaveChangesAsync(ct);
    }
}
