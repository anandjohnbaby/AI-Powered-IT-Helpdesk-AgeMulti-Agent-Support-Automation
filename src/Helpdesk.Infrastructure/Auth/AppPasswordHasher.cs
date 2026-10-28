using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.AspNetCore.Identity;

namespace Helpdesk.Infrastructure.Auth;

/// ASP.NET Core Identity's hasher: PBKDF2 with HMAC-SHA512, salted, many iterations.
public class AppPasswordHasher : IAppPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string Hash(AppUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(AppUser user, string password)
        => _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}