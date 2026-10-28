using Helpdesk.Application.Agent.Plugins;
using Helpdesk.Application.Common;
using Helpdesk.Application.Guardrails;
using Helpdesk.Application.Services;
using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Helpdesk.Application.Agent;

public record ChatReply(string Reply, bool Blocked);

public class HelpdeskAgent(
    IOptions<LlmOptions> llm, ICurrentUser user, IAppDbContext db,
    InputGuard inputGuard, OutputGuard outputGuard, AuditLogger audit,
    AccountPlugin accountPlugin, ServerPlugin serverPlugin, TicketPlugin ticketPlugin,
    ApprovalGateFilter approvalGate)
{
    public async Task<ChatReply> ChatAsync(string message, CancellationToken ct = default)
    {
        var username = user.Username;
        db.ChatMessages.Add(new ChatMessage { Username = username, Role = "user", Content = message });

        var check = inputGuard.Check(message);
        if (!check.Allowed)
        {
            var blocked = $"I can't help with that request. ({check.Reason})";
            db.ChatMessages.Add(new ChatMessage { Username = username, Role = "assistant", Content = blocked });
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(username, "input_guard", message, $"Blocked: {check.Reason}", ct);
            return new ChatReply(blocked, true);
        }
        await db.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(llm.Value.ApiKey))
            return await SaveReply(username, "The AI service is not configured (missing API key).", ct);

        var history = new ChatHistory(SystemPrompt.Build(username));
        var recent = await db.ChatMessages.Where(m => m.Username == username)
            .OrderByDescending(m => m.Id).Take(12).OrderBy(m => m.Id).ToListAsync(ct);
        foreach (var m in recent)
        {
            if (m.Role == "user") history.AddUserMessage(m.Content);
            else history.AddAssistantMessage(m.Content);
        }

        var kernel = BuildKernel();
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.2
        };

        string reply;
        try
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var result = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
            reply = result.Content ?? "Sorry, I couldn't generate a response.";
        }
        catch (HttpOperationException ex)
        {
            await audit.LogAsync(username, "llm_call", ex.Message, "Error", ct);
            reply = "The AI service is temporarily unavailable. Please try again in a moment.";
        }

        return await SaveReply(username, outputGuard.Sanitize(reply), ct);
    }

    private Kernel BuildKernel()
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(llm.Value.Model, new Uri(llm.Value.Endpoint), llm.Value.ApiKey);
        var kernel = builder.Build();

        kernel.Plugins.AddFromObject(accountPlugin, "account");
        kernel.Plugins.AddFromObject(serverPlugin, "servers");
        kernel.Plugins.AddFromObject(ticketPlugin, "tickets");
        kernel.AutoFunctionInvocationFilters.Add(approvalGate);
        return kernel;
    }

    private async Task<ChatReply> SaveReply(string username, string reply, CancellationToken ct)
    {
        db.ChatMessages.Add(new ChatMessage { Username = username, Role = "assistant", Content = reply });
        await db.SaveChangesAsync(ct);
        return new ChatReply(reply, false);
    }
}
