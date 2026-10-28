using System.Text.Json;
using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.Extensions.Options;

namespace Helpdesk.Application.Services;

/// Deterministic code that performs high-risk actions ONLY after human approval.
public class ActionExecutor(IIdentityService directory, IServerService servers, IEmailService email,
    IOptions<HelpdeskOptions> options)
{
    public const string ResetPassword = "reset_my_password";
    public const string RestartServer = "restart_server";

    public async Task<string?> ValidateAsync(string action, string? directoryAccount,
        IDictionary<string, string> args, CancellationToken ct)
    {
        switch (action)
        {
            case ResetPassword:
                if (string.IsNullOrEmpty(directoryAccount))
                    return "You have no linked directory (Keycloak) account.";
                return await directory.GetUserAsync(directoryAccount, ct) is null
                    ? $"Directory account '{directoryAccount}' was not found." : null;
            case RestartServer:
                var name = args.TryGetValue("serverName", out var n) ? n : "";
                return await servers.GetAsync(name, ct) is null
                    ? $"'{name}' is not a managed server. Use list_servers to see valid names." : null;
            default:
                return $"Action '{action}' is not supported by the approval workflow.";
        }
    }

    public async Task<string> ExecuteAsync(ApprovalRequest req, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(req.ArgumentsJson) ?? new();

        switch (req.ActionName)
        {
            case ResetPassword:
                return await ResetAndNotifyAsync(req.DirectoryAccount, $"Approval request {req.Id}", ct);
            case RestartServer:
                {
                    var name = args["serverName"];
                    await servers.RestartAsync(name, ct);
                    var s = await servers.GetAsync(name, ct);
                    return $"Server '{name}' was restarted. Current state: {s?.State ?? "unknown"}.";
                }
            default:
                throw new InvalidOperationException($"Unsupported action '{req.ActionName}'.");
        }
    }

    /// Resets the Keycloak password to a temporary one and emails it. Keycloak forces a new password at next login.
    public async Task<string> ResetAndNotifyAsync(string directoryAccount, string reference, CancellationToken ct)
    {
        var user = await directory.GetUserAsync(directoryAccount, ct)
                   ?? throw new InvalidOperationException("Directory account no longer exists.");
        var temp = await directory.ResetPasswordAsync(directoryAccount, ct);

        await email.SendAsync(user.Email, "Your temporary directory password",
            $"Hello {user.FullName},\n\n" +
            "Your directory (Keycloak) password was reset.\n" +
            $"Temporary password: {temp}\n\n" +
            $"Sign in here: {options.Value.AccountConsoleUrl}\n" +
            "You will be asked to choose a new password immediately.\n\n" +
            "Note: this does NOT change your HelpBot portal password.\n\n" +
            $"Reference: {reference}", ct);

        return $"Directory password reset. A temporary password was emailed to {Mask(user.Email)}.";
    }

    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" + email[Math.Max(at, 0)..] : email[0] + "***" + email[at..];
    }
}