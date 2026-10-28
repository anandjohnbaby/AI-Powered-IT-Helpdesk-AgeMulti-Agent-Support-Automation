using System.Text;
using Helpdesk.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Api.Extensions;

public static class AuthenticationExtensions
{
    public const string EmployeeOnly = "EmployeeOnly";
    public const string ApproverOnly = "ApproverOnly";

    public static IServiceCollection AddAppJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be set (at least 32 characters). Use dotnet user-secrets.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // keep claim names as issued: sub, name, role, portal, jti
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                o.Events = new JwtBearerEvents
                {
                    // Reject tokens that were logged out (server-side revocation)
                    OnTokenValidated = ctx =>
                    {
                        var store = ctx.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();
                        var jti = ctx.Principal?.FindFirst("jti")?.Value;
                        if (jti is null || store.IsRevoked(jti)) ctx.Fail("Token has been revoked.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(EmployeeOnly, p => p.RequireRole(Roles.Employee).RequireClaim("portal", Portals.Employee));
            o.AddPolicy(ApproverOnly, p => p.RequireRole(Roles.Approver).RequireClaim("portal", Portals.It));
            // Deny by default: every endpoint needs a valid token unless marked [AllowAnonymous]
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        return services;
    }
}
