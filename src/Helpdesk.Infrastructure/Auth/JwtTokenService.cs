using System.Security.Claims;
using System.Text;
using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Infrastructure.Auth;

/// Issues the portal's own JWT (HS256).
public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public IssuedToken CreateToken(AppUser user, string portal)
    {
        var o = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(o.ExpiryMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("sub", user.Username),
            new("name", user.FullName),
            new("role", user.Role),
            new("portal", portal),
            new("jti", jti)
        };
        if (!string.IsNullOrEmpty(user.DirectoryAccount))
            claims.Add(new Claim("dir", user.DirectoryAccount)); // linked Keycloak account

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), jti, expires);
    }
}
