using DN.WebApi.Infrastructure.Identity.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Host.IntegrationTests.Mocks;

public static class TokenServiceMock
{
    public static string Issuer { get; } = Guid.NewGuid().ToString();
    public static SecurityKey SecurityKey { get; }
    public static SigningCredentials SigningCredentials { get; }

    private static readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private static readonly byte[] _key = new byte[32];

    static TokenServiceMock()
    {
        _rng.GetBytes(_key);
        SecurityKey = new SymmetricSecurityKey(_key) { KeyId = Guid.NewGuid().ToString() };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
    }

    public static string GenerateJwtToken(IEnumerable<Claim> claims)
    {
        return _tokenHandler.WriteToken(new JwtSecurityToken(Issuer, null, claims, null, DateTime.UtcNow.AddMinutes(20), SigningCredentials));
    }

    public static IEnumerable<Claim> GetClaims(ApplicationUser user, string ipAddress)
    {
        return new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, user.Email),
                new("fullName", $"{user.FirstName} {user.LastName}"),
                new(ClaimTypes.Name, user.FirstName ?? string.Empty),
                new(ClaimTypes.Surname, user.LastName ?? string.Empty),
                new("ipAddress", ipAddress),
                new("tenant", user.Tenant ?? string.Empty)
            };
    }
}