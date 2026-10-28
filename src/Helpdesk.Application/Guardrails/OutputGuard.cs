using System.Text.RegularExpressions;

namespace Helpdesk.Application.Guardrails;

/// Redacts secrets if the LLM ever tries to output them.
public class OutputGuard
{
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        (new(@"(?i)\b(password|passwd|pwd)\s*(is|:|=)\s*\S+"), "$1 $2 [REDACTED]"),
        (new(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+"), "[REDACTED_TOKEN]"),
        (new(@"(?i)\b(api[_-]?key|secret|bearer)\s*[:=]?\s*[A-Za-z0-9_\-\.]{16,}"), "$1 [REDACTED]"),
    ];

    public string Sanitize(string text)
    {
        foreach (var (pattern, replacement) in Rules)
            text = pattern.Replace(text, replacement);
        return text;
    }
}
