using Helpdesk.Application.Agent;
using Helpdesk.Application.Agent.Plugins;
using Helpdesk.Application.Auth;
using Helpdesk.Application.Guardrails;
using Helpdesk.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<InputGuard>();
        services.AddSingleton<OutputGuard>();
        services.AddScoped<AuditLogger>();
        services.AddScoped<AuthService>();
        services.AddScoped<ActionExecutor>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<DirectoryAdminService>();
        services.AddScoped<AccountPlugin>();
        services.AddScoped<ServerPlugin>();
        services.AddScoped<TicketPlugin>();
        services.AddScoped<ApprovalGateFilter>();
        services.AddScoped<HelpdeskAgent>();
        return services;
    }
}