using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServiceDefaults.Auth;
using UserService.Models;

namespace UserService.Security;

/// <summary>Issues signed JWTs that the other services validate with the shared key.</summary>
public sealed class TokenService(JwtOptions options)
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials =
        new(AuthenticationExtensions.CreateSigningKey(options.SigningKey), SecurityAlgorithms.HmacSha256);

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(options.LifetimeHours);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(AppClaims.Name, user.Name),
                new Claim(AppClaims.Role, user.Role)
            ])
        });
        return (token, expiresAt);
    }
}
