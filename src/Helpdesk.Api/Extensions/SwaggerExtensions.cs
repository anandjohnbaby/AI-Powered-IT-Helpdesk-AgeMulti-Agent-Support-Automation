using Microsoft.OpenApi;

namespace Helpdesk.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "HelpBot – IT Helpdesk Agent API",
                Version = "v1",
                Description = "1) Call POST /api/auth/login  2) Copy accessToken  3) Click Authorize and paste it."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken returned by /api/auth/login (without the word Bearer)."
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        return services;
    }

    public static WebApplication UseSwaggerWithJwt(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HelpBot API v1"));
        return app;
    }
}
