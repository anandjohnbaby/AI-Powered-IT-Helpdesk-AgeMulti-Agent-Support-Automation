using System.Text.RegularExpressions;

namespace Helpdesk.Application.Guardrails;

public record GuardResult(bool Allowed, string? Reason = null);

/// Blocks obvious prompt-injection before it reaches the LLM.
public class InputGuard
{
    private const int MaxLength = 1000;

    private static readonly (Regex Pattern, string Reason)[] Rules =
    [
        (R(@"ignore\s+(all\s+)?(previous|prior|above|your|the)\s+(instructions|rules|prompts?)"), "Attempt to override agent instructions"),
        (R(@"(reveal|show|print|repeat).{0,30}(system\s+prompt|your\s+instructions)"), "Attempt to extract the system prompt"),
        (R(@"(bypass|skip|disable|without).{0,20}(approval|guardrails?|security|verification)"), "Attempt to bypass approval"),
        (R(@"\b(you\s+are\s+now|pretend\s+(to\s+be|you\s+are)|act\s+as)\b.{0,30}\b(admin|administrator|root|developer|system)"), "Role-escalation attempt"),
        (R(@"\b(developer|god|jailbreak|dan)\s+mode\b"), "Jailbreak attempt"),
    ];

    private static Regex R(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public GuardResult Check(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new(false, "Empty message");
        if (input.Length > MaxLength) return new(false, $"Message longer than {MaxLength} characters");

        foreach (var (pattern, reason) in Rules)
            if (pattern.IsMatch(input)) return new(false, reason);

        return new(true);
    }
}
