using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Helpdesk.Application.Common;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Services;

public class KeycloakOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "helpdesk";
    public string ClientId { get; set; } = "helpdesk-backend";
    public string ClientSecret { get; set; } = "";
}

/// Manages the Keycloak directory via the Admin REST API using a least-privilege service account.
public class KeycloakIdentityService(HttpClient http, IOptions<KeycloakOptions> options) : IIdentityService
{
    private static string? _cachedToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private KeycloakOptions O => options.Value;

    public async Task<IReadOnlyList<DirectoryUser>> ListUsersAsync(CancellationToken ct = default)
    {
        using var res = await http.SendAsync(await BuildAsync(HttpMethod.Get, "users?max=100", ct), ct);
        res.EnsureSuccessStatusCode();
        var arr = await res.Content.ReadFromJsonAsync<JsonElement>(ct);

        var users = new List<DirectoryUser>();
        foreach (var u in arr.EnumerateArray())
        {
            var username = u.GetProperty("username").GetString() ?? "";
            if (username.StartsWith("service-account-")) continue;
            users.Add(await ToDirectoryUserAsync(u, ct));
        }
        return users.OrderBy(u => u.Username).ToList();
    }

    public async Task<DirectoryUser?> GetUserAsync(string username, CancellationToken ct = default)
    {
        var user = await FindUserAsync(username, ct);
        return user is null ? null : await ToDirectoryUserAsync(user.Value, ct);
    }

    public async Task UnlockAsync(string username, CancellationToken ct = default)
    {
        var id = await GetIdAsync(username, ct);
        using var res = await http.SendAsync(await BuildAsync(HttpMethod.Delete, $"attack-detection/brute-force/users/{id}", ct), ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<string> ResetPasswordAsync(string username, CancellationToken ct = default)
    {
        var id = await GetIdAsync(username, ct);
        var temp = RandomNumberGenerator.GetString(
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%", 14);

        // temporary=true -> Keycloak itself forces "Update password" on the next Keycloak login
        var body = new { type = "password", value = temp, temporary = true };
        using var res = await http.SendAsync(await BuildAsync(HttpMethod.Put, $"users/{id}/reset-password", ct, body), ct);
        res.EnsureSuccessStatusCode();

        // Clear any lockout so the temporary password works immediately
        using var unlock = await http.SendAsync(await BuildAsync(HttpMethod.Delete, $"attack-detection/brute-force/users/{id}", ct), ct);
        return temp;
    }

    // ---------- helpers ----------
    private async Task<DirectoryUser> ToDirectoryUserAsync(JsonElement u, CancellationToken ct)
    {
        var id = u.GetProperty("id").GetString();
        using var res = await http.SendAsync(await BuildAsync(HttpMethod.Get, $"attack-detection/brute-force/users/{id}", ct), ct);
        res.EnsureSuccessStatusCode();
        var bf = await res.Content.ReadFromJsonAsync<JsonElement>(ct);

        var first = u.TryGetProperty("firstName", out var f) ? f.GetString() : "";
        var last = u.TryGetProperty("lastName", out var l) ? l.GetString() : "";

        return new DirectoryUser(
            u.GetProperty("username").GetString() ?? "",
            $"{first} {last}".Trim(),
            u.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
            u.GetProperty("enabled").GetBoolean(),
            bf.TryGetProperty("disabled", out var d) && d.GetBoolean(),
            bf.TryGetProperty("numFailures", out var n) ? n.GetInt32() : 0);
    }

    private async Task<string> GetIdAsync(string username, CancellationToken ct)
        => (await FindUserAsync(username, ct))?.GetProperty("id").GetString()
           ?? throw new InvalidOperationException($"Directory account '{username}' not found.");

    private async Task<JsonElement?> FindUserAsync(string username, CancellationToken ct)
    {
        using var res = await http.SendAsync(await BuildAsync(HttpMethod.Get,
            $"users?username={Uri.EscapeDataString(username)}&exact=true", ct), ct);
        res.EnsureSuccessStatusCode();
        var arr = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        return arr.GetArrayLength() > 0 ? arr[0] : (JsonElement?)null;
    }

    private async Task<HttpRequestMessage> BuildAsync(HttpMethod method, string path, CancellationToken ct, object? body = null)
    {
        var req = new HttpRequestMessage(method, $"{O.BaseUrl}/admin/realms/{O.Realm}/{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetServiceTokenAsync(ct));
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task<string> GetServiceTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt) return _cachedToken;

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt) return _cachedToken;

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = O.ClientId,
                ["client_secret"] = O.ClientSecret
            });
            using var res = await http.PostAsync($"{O.BaseUrl}/realms/{O.Realm}/protocol/openid-connect/token", form, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);

            _cachedToken = json.GetProperty("access_token").GetString()!;
            var expiresIn = json.GetProperty("expires_in").GetInt32();
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 30, 10));
            return _cachedToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }
}
