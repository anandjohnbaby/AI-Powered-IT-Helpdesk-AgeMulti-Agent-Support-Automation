using System.Text.Json;
using Helpdesk.Application.Common;
using Helpdesk.Application.Guardrails;
using Helpdesk.Application.Services;
using Microsoft.SemanticKernel;

namespace Helpdesk.Application.Agent;

/// Intercepts EVERY tool call the LLM makes. Enforced in code, so it cannot be prompt-injected away.
public class ApprovalGateFilter(ICurrentUser user, ApprovalService approvals, AuditLogger audit)
    : IAutoFunctionInvocationFilter
{
    private const int MaxModelRoundTrips = 5;

    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var fn = context.Function.Name;
        var args = context.Arguments?.ToDictionary(a => a.Key, a => a.Value?.ToString() ?? "")
                   ?? new Dictionary<string, string>();
        var argsJson = JsonSerializer.Serialize(args);

        if (context.RequestSequenceIndex >= MaxModelRoundTrips)
        {
            context.Result = new FunctionResult(context.Function, "Tool call limit reached for this message.");
            context.Terminate = true;
            return;
        }

        if (!RiskPolicy.IsKnown(fn))
        {
            await audit.LogAsync(user.Username, fn, argsJson, "Blocked-UnknownTool");
            context.Result = new FunctionResult(context.Function, "BLOCKED: this tool is not on the allow-list.");
            return;
        }

        var risk = RiskPolicy.Get(fn);
        if (RiskPolicy.RequiresApproval(risk))
        {
            var message = await approvals.RequestAsync(user.Username, user.DirectoryAccount, fn, args, risk);
            context.Result = new FunctionResult(context.Function, message);
            return;
        }

        try
        {
            await next(context);
            await audit.LogAsync(user.Username, fn, argsJson, "Executed");
        }
        catch (Exception ex)
        {
            await audit.LogAsync(user.Username, fn, $"{argsJson} | {ex.Message}", "Error");
            context.Result = new FunctionResult(context.Function, $"ERROR: {ex.Message}");
        }
    }
}