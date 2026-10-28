using System.Text.Json;
using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Application.Services;

public class ApprovalService(IAppDbContext db, ActionExecutor executor, AuditLogger audit,
    IOptions<HelpdeskOptions> options)
{
    public static string Short(Guid id) => id.ToString()[..8];

    public async Task<string> RequestAsync(string requester, string? directoryAccount, string action,
        Dictionary<string, string> args, RiskLevel risk, CancellationToken ct = default)
    {
        var error = await executor.ValidateAsync(action, directoryAccount, args, ct);
        if (error is not null)
        {
            await audit.LogAsync(requester, action, error, "Rejected-Validation", ct);
            return $"Cannot request this action: {error}";
        }

        var argsJson = JsonSerializer.Serialize(args);

        var existing = await db.Approvals.FirstOrDefaultAsync(a =>
            a.Requester == requester && a.ActionName == action &&
            a.ArgumentsJson == argsJson && a.Status == ApprovalStatus.Pending, ct);
        if (existing is not null)
            return $"APPROVAL_PENDING: An identical request ({Short(existing.Id)}) is already waiting for IT approval.";

        var req = new ApprovalRequest
        {
            Requester = requester,
            DirectoryAccount = directoryAccount ?? "",
            ActionName = action,
            ArgumentsJson = argsJson,
            Risk = risk,
            Reason = args.GetValueOrDefault("reason") ?? "",
            ExpiresAt = DateTime.UtcNow.AddMinutes(options.Value.ApprovalExpiryMinutes)
        };
        db.Approvals.Add(req);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(requester, action, $"Approval {req.Id} created. Args: {argsJson}", "PendingApproval", ct);

        return $"APPROVAL_REQUIRED: Request {Short(req.Id)} was created and is waiting for an IT administrator. " +
               "The action has NOT been performed yet.";
    }

    public async Task<List<ApprovalRequest>> ListAsync(CancellationToken ct = default)
    {
        await ExpireStaleAsync(ct);
        return await db.Approvals.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<List<ApprovalRequest>> ListForUserAsync(string username, CancellationToken ct = default)
    {
        await ExpireStaleAsync(ct);
        return await db.Approvals.Where(a => a.Requester == username)
            .OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync(ct);
    }

    public async Task<ApprovalRequest> DecideAsync(Guid id, string approver, bool approve, string? note,
        CancellationToken ct = default)
    {
        await ExpireStaleAsync(ct);
        var req = await db.Approvals.FirstOrDefaultAsync(a => a.Id == id, ct)
                  ?? throw new KeyNotFoundException("Approval request not found.");

        if (req.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Request is already {req.Status}.");
        if (string.Equals(req.Requester, approver, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("Separation of duties: you cannot approve your own request.");
        if (!approve && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A note is required when rejecting a request.");

        req.Approver = approver;
        req.DecisionNote = note;
        req.DecidedAt = DateTime.UtcNow;

        if (!approve)
        {
            req.Status = ApprovalStatus.Rejected;
            req.Result = "Rejected by IT.";
        }
        else
        {
            try
            {
                req.Result = await executor.ExecuteAsync(req, ct);
                req.Status = ApprovalStatus.Executed;
            }
            catch (Exception ex)
            {
                req.Status = ApprovalStatus.Failed;
                req.Result = "Execution failed: " + ex.Message;
            }
        }

        db.ChatMessages.Add(new ChatMessage { Username = req.Requester, Role = "assistant", Content = Notification(req) });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(approver, $"decision:{req.ActionName}",
            $"Request {req.Id} -> {req.Status}. Note: {note}", req.Status.ToString(), ct);
        return req;
    }

    private async Task ExpireStaleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stale = await db.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt < now).ToListAsync(ct);
        if (stale.Count == 0) return;
        foreach (var a in stale) { a.Status = ApprovalStatus.Expired; a.Result = "Expired without a decision."; }
        await db.SaveChangesAsync(ct);
    }

    private static string Notification(ApprovalRequest r) => r.Status switch
    {
        ApprovalStatus.Executed => $"Update on request {Short(r.Id)}: approved by IT. {r.Result}",
        ApprovalStatus.Rejected => $"Update on request {Short(r.Id)}: rejected by IT. Reason: {r.DecisionNote}",
        _ => $"Update on request {Short(r.Id)}: approved, but execution failed. IT has been notified."
    };
}