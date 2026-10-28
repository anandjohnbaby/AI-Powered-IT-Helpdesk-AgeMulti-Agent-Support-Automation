using System.ComponentModel;
using Helpdesk.Application.Common;
using Microsoft.SemanticKernel;

namespace Helpdesk.Application.Agent.Plugins;

// Acts ONLY on the signed-in user's linked directory account (from the JWT). No username parameter exists.
public class AccountPlugin(ICurrentUser user, IIdentityService directory)
{
    private const string NoAccount = "The signed-in user has no linked directory (Keycloak) account. Offer to create a ticket.";

    [KernelFunction("get_my_account_status")]
    [Description("Gets the status of the signed-in user's directory (Keycloak) account: enabled, locked, failed logins.")]
    public async Task<string> GetMyAccountStatusAsync(CancellationToken ct = default)
    {
        if (user.DirectoryAccount is not { } account) return NoAccount;
        var u = await directory.GetUserAsync(account, ct);
        return u is null
            ? "Directory account not found."
            : $"Directory account: {u.Username}, Enabled: {u.Enabled}, Locked: {u.Locked}, Failed login attempts: {u.FailedAttempts}";
    }

    [KernelFunction("unlock_my_account")]
    [Description("Unlocks the signed-in user's directory (Keycloak) account if it was locked after failed logins.")]
    public async Task<string> UnlockMyAccountAsync(CancellationToken ct = default)
    {
        if (user.DirectoryAccount is not { } account) return NoAccount;
        var u = await directory.GetUserAsync(account, ct);
        if (u is null) return "Directory account not found.";
        if (!u.Locked) return "The directory account is not locked, so nothing was changed.";
        await directory.UnlockAsync(account, ct);
        return "The directory account has been unlocked. The user can sign in to Keycloak again.";
    }

    [KernelFunction("reset_my_password")]
    [Description("Requests a reset of the signed-in user's directory (Keycloak) password. HIGH RISK: needs IT approval; " +
                 "a temporary password is emailed after approval. Does not affect the HelpBot portal password.")]
    public string ResetMyPassword([Description("Why the user needs a reset")] string reason = "")
        => throw new InvalidOperationException("High-risk action: must go through the approval workflow.");
}