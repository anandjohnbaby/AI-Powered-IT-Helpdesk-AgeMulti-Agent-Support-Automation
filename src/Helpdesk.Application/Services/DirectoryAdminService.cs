using Helpdesk.Application.Common;

namespace Helpdesk.Application.Services;

/// Direct directory management for IT staff (the human IS the approver here). Every action needs a reason and is audited.
public class DirectoryAdminService(IIdentityService directory, ActionExecutor executor, AuditLogger audit, ICurrentUser currentUser)
{
    public Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default)
        => directory.ListUsersAsync(ct);

    public async Task<string> UnlockAsync(string username, string? reason, CancellationToken ct = default)
    {
        RequireReason(reason);
        var user = await directory.GetUserAsync(username, ct)
                   ?? throw new KeyNotFoundException("Directory account not found.");
        if (!user.Locked) return $"{username} is not locked.";

        await directory.UnlockAsync(username, ct);
        await audit.LogAsync(currentUser.Username, "it_unlock", $"account={username}; reason={reason}", "Executed", ct);
        return $"{username} has been unlocked.";
    }

    public async Task<string> ResetPasswordAsync(string username, string? reason, CancellationToken ct = default)
    {
        RequireReason(reason);
        _ = await directory.GetUserAsync(username, ct) ?? throw new KeyNotFoundException("Directory account not found.");

        var result = await executor.ResetAndNotifyAsync(username, $"Direct reset by IT ({currentUser.Username})", ct);
        await audit.LogAsync(currentUser.Username, "it_reset_password", $"account={username}; reason={reason}", "Executed", ct);
        return result;
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new InvalidOperationException("A reason (at least 5 characters) is required for the audit log.");
    }
}