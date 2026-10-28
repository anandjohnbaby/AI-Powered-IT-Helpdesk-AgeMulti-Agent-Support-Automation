using System.Collections.Concurrent;
using Helpdesk.Application.Common;

namespace Helpdesk.Infrastructure.Auth;

/// Denylist of logged-out token IDs (jti). Entries are dropped once the token would have expired anyway.
/// In production this would live in Redis so it works across multiple API instances.
public class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new();

    public void Revoke(string jti, DateTime expiresAt)
    {
        _revoked[jti] = expiresAt;
        var now = DateTime.UtcNow;
        foreach (var entry in _revoked)
            if (entry.Value < now) _revoked.TryRemove(entry.Key, out _);
    }

    public bool IsRevoked(string jti) => _revoked.ContainsKey(jti);
}
