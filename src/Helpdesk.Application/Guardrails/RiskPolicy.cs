using Helpdesk.Domain;

namespace Helpdesk.Application.Guardrails;

/// Central allow-list: every tool the agent can call MUST be classified here.
public static class RiskPolicy
{
    private static readonly Dictionary<string, RiskLevel> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["list_servers"] = RiskLevel.Low,
        ["check_server_status"] = RiskLevel.Low,
        ["get_my_account_status"] = RiskLevel.Low,
        ["create_ticket"] = RiskLevel.Low,
        ["list_my_tickets"] = RiskLevel.Low,
        ["list_my_approval_requests"] = RiskLevel.Low,
        ["unlock_my_account"] = RiskLevel.Medium,
        ["reset_my_password"] = RiskLevel.High,
        ["restart_server"] = RiskLevel.High,
    };

    public static bool IsKnown(string function) => Map.ContainsKey(function);
    public static RiskLevel Get(string function) => Map.TryGetValue(function, out var r) ? r : RiskLevel.Critical;
    public static bool RequiresApproval(RiskLevel risk) => risk >= RiskLevel.High;
}
