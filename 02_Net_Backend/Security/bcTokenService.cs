using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Commerce.Api.Data;
using Commerce.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Api.Security;

/// <summary>Genera access tokens JWT y refresh tokens opacos.</summary>
public class bcTokenService(IOptions<bcJwtOptions> options, TimeProvider clock)
{
    private readonly bcJwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(bcAppUser user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
            }),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    /// <summary>512 bits aleatorios criptográficamente seguros, en base64url.</summary>
    public string GenerateRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    /// <summary>En base de datos solo se guarda el hash SHA-256 (64 caracteres hex).</summary>
    public static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
