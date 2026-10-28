namespace Helpdesk.Domain;

public enum RiskLevel { Low, Medium, High, Critical }

public enum ApprovalStatus { Pending, Approved, Rejected, Expired, Executed, Failed }

/// HelpBot portal account (independent of the Keycloak directory account).
public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "employee";
    public string? DirectoryAccount { get; set; }   // linked Keycloak username (null for IT staff)
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Requester { get; set; } = "";          // portal username
    public string DirectoryAccount { get; set; } = "";   // Keycloak account the action targets
    public string ActionName { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
    public string Reason { get; set; } = "";
    public RiskLevel Risk { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Approver { get; set; }
    public string? DecisionNote { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public DateTime? DecidedAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public string Outcome { get; set; } = "";
}

public class ChatMessage
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Ticket
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}