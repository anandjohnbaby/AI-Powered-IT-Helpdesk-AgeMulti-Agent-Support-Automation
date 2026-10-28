using Helpdesk.Application.Common;
using Helpdesk.Infrastructure.Auth;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LlmOptions>(config.GetSection("Llm"));
        services.Configure<HelpdeskOptions>(config.GetSection("Helpdesk"));
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<KeycloakOptions>(config.GetSection("Keycloak"));
        services.Configure<DockerOptions>(config.GetSection("Docker"));
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));

        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=helpdesk.db"));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IAppPasswordHasher, AppPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();

        services.AddHttpClient<IIdentityService, KeycloakIdentityService>();
        services.AddSingleton<IServerService, DockerServerService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        return services;
    }
}