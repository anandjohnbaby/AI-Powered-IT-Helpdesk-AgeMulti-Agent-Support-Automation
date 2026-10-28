using Helpdesk.Application.Common;
using Helpdesk.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Application.Auth;

public record LoginRequest(string Username, string Password, string Portal);
public record LoginResult(string AccessToken, DateTime ExpiresAt, string Username, string FullName,
    string Role, string Portal, string? DirectoryAccount);

/// Portal login against the app's OWN user store. Completely independent of Keycloak.
public class AuthService(IAppDbContext db, IAppPasswordHasher hasher, ITokenService tokens,
    ITokenRevocationStore revocations, AuditLogger audit)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = (request.Username ?? "").Trim().ToLowerInvariant();
        var portal = (request.Portal ?? "").Trim().ToLowerInvariant();

        if (username.Length == 0 || string.IsNullOrEmpty(request.Password))
            throw new AuthenticationFailedException("Username and password are required.");
        if (portal is not (Portals.Employee or Portals.It))
            throw new InvalidOperationException("Unknown portal.");

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            await audit.LogAsync(username, "portal_login", $"portal={portal}", "Failed-UnknownUser", ct);
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        if (user.LockoutEndUtc > DateTime.UtcNow)
        {
            await audit.LogAsync(username, "portal_login", $"portal={portal}", "Failed-Locked", ct);
            var minutes = Math.Ceiling((user.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            throw new AuthenticationFailedException($"Too many failed attempts. Try again in {minutes} minute(s).");
        }

        if (!hasher.Verify(user, request.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(username, "portal_login", $"portal={portal}", "Failed-BadPassword", ct);
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync(ct);

        // Portal separation: employees -> chat portal, IT approvers -> IT portal
        var requiredRole = portal == Portals.It ? Roles.Approver : Roles.Employee;
        if (user.Role != requiredRole)
        {
            await audit.LogAsync(username, "portal_login", $"portal={portal}", "Denied-WrongPortal", ct);
            throw new ForbiddenAccessException(portal == Portals.It
                ? "This account is not authorized for the IT portal."
                : "IT staff must sign in through the IT portal.");
        }

        var issued = tokens.CreateToken(user, portal);
        await audit.LogAsync(username, "portal_login", $"portal={portal}, jti={issued.Jti}", "Success", ct);

        return new LoginResult(issued.Token, issued.ExpiresAt, user.Username, user.FullName,
            user.Role, portal, user.DirectoryAccount);
    }

    public async Task LogoutAsync(string username, string jti, DateTime expiresAt, CancellationToken ct = default)
    {
        revocations.Revoke(jti, expiresAt);
        await audit.LogAsync(username, "portal_logout", $"jti={jti}", "Success", ct);
    }
}