using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Application.Common;

public interface IAppDbContext
{
    DbSet<AppUser> AppUsers { get; }
    DbSet<ApprovalRequest> Approvals { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Ticket> Tickets { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// The authenticated portal user, resolved from the validated JWT.
public interface ICurrentUser
{
    string Username { get; }
    string? DirectoryAccount { get; }
    bool IsApprover { get; }
}

public interface IAppPasswordHasher
{
    string Hash(AppUser user, string password);
    bool Verify(AppUser user, string password);
}

public record IssuedToken(string Token, string Jti, DateTime ExpiresAt);

public interface ITokenService
{
    IssuedToken CreateToken(AppUser user, string portal);
}

public interface ITokenRevocationStore
{
    void Revoke(string jti, DateTime expiresAt);
    bool IsRevoked(string jti);
}

public record DirectoryUser(string Username, string FullName, string Email, bool Enabled, bool Locked, int FailedAttempts);

/// The corporate directory (Keycloak) that the helpdesk MANAGES. Not used for portal login.
public interface IIdentityService
{
    Task<IReadOnlyList<DirectoryUser>> ListUsersAsync(CancellationToken ct = default);
    Task<DirectoryUser?> GetUserAsync(string username, CancellationToken ct = default);
    Task UnlockAsync(string username, CancellationToken ct = default);
    Task<string> ResetPasswordAsync(string username, CancellationToken ct = default); // returns temporary password
}

public record ServerInfo(string Name, string Image, string State, string Status);

public interface IServerService
{
    Task<IReadOnlyList<ServerInfo>> ListAsync(CancellationToken ct = default);
    Task<ServerInfo?> GetAsync(string name, CancellationToken ct = default);
    Task RestartAsync(string name, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
