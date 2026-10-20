using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServiceDefaults.Auth;

namespace TestCommon;

/// <summary>Creates JWTs signed with the Development key, exactly like User Service does.</summary>
public static class TestTokens
{
    private static readonly JwtOptions Options = new() { SigningKey = JwtOptions.DevelopmentSigningKey };

    public static string Create(string userId, string name, string role, DateTime? expires = null)
    {
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Options.Issuer,
            Audience = Options.Audience,
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            NotBefore = (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
            SigningCredentials = new SigningCredentials(
                AuthenticationExtensions.CreateSigningKey(Options.SigningKey), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, userId),
                new Claim(AppClaims.Name, name),
                new Claim(AppClaims.Role, role)
            ])
        });
    }

    public static string Employee(string id = "1001", string name = "Meric") => Create(id, name, Roles.Employee);
    public static string ItSupport(string id = "1002", string name = "Alex") => Create(id, name, Roles.ItSupport);

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
