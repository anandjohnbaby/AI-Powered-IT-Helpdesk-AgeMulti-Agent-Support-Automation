using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Helpdesk.Api.Auth;
using Helpdesk.Api.Extensions;
using Helpdesk.Application;
using Helpdesk.Application.Common;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAppJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy("chat", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Create DB + seed portal accounts
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await DbSeeder.SeedAsync(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IAppPasswordHasher>());
}

if (app.Environment.IsDevelopment())
    app.UseSwaggerWithJwt();

app.UseCors();

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var (status, code) = ex switch
        {
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "AUTH_FAILED"),
            ForbiddenAccessException or UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "FORBIDDEN"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "BAD_REQUEST"),
            _ => (StatusCodes.Status500InternalServerError, "SERVER_ERROR")
        };
        if (status == 500) app.Logger.LogError(ex, "Unhandled error");
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = status == 500 ? "Internal server error" : ex.Message, code });
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();