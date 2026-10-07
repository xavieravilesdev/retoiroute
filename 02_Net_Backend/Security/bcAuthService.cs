using Commerce.Api.Data;
using Commerce.Api.Dtos;
using Commerce.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Security;

/// <summary>Login, renovación con rotación de refresh tokens y logout.</summary>
public class bcAuthService(
    bcCommerceDbContext db,
    bcTokenService tokens,
    IOptions<bcJwtOptions> jwtOptions,
    TimeProvider clock,
    ILogger<bcAuthService> logger)
{
    // Se verifica siempre un hash, exista o no el usuario, para no revelar usuarios por tiempo de respuesta.
    private static readonly string DummyHash = stPasswordHasher.Hash("no-es-una-contraseña-real");

    private readonly bcJwtOptions _jwt = jwtOptions.Value;

    /// <returns>Los tokens, o null si las credenciales no son válidas.</returns>
    public async Task<DtoAuthResponse?> LoginAsync(DtoLoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        var passwordOk = stPasswordHasher.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !user.IsActive || !passwordOk)
        {
            logger.LogWarning("Intento de login fallido");
            return null;
        }

        return await MeIssueTokensAsync(user, ct);
    }

    /// <summary>
    /// Canjea un refresh token por un nuevo par de tokens (rotación: el anterior queda revocado).
    /// Si se reutiliza un token ya revocado se asume robo y se revocan todas las sesiones del usuario.
    /// </summary>
    /// <returns>Los nuevos tokens, o null si el refresh token no es válido.</returns>
    public async Task<DtoAuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = bcTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return null;

        var now = clock.GetUtcNow().UtcDateTime;

        if (stored.RevokedAt is not null)
        {
            await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            logger.LogWarning("Reutilización de refresh token detectada para el usuario {UserId}", stored.UserId);
            return null;
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive) return null;

        stored.RevokedAt = now;
        return await MeIssueTokensAsync(stored.User, ct);
    }

    /// <summary>Revoca el refresh token indicado (solo si pertenece al usuario autenticado). Es idempotente.</summary>
    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken ct)
    {
        var hash = bcTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
        if (stored is null || stored.RevokedAt is not null) return;

        stored.RevokedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    private async Task<DtoAuthResponse> MeIssueTokensAsync(bcAppUser user, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var (accessToken, expiresAt) = tokens.CreateAccessToken(user);
        var refreshToken = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new bcRefreshToken
        {
            UserId = user.Id,
            TokenHash = bcTokenService.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
        });

        // Un solo SaveChanges: guarda el token nuevo y la revocación del anterior (si la hubo).
        await db.SaveChangesAsync(ct);

        // Limpieza de tokens vencidos del usuario.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        return new DtoAuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Username = user.Username,
            Role = user.Role,
        };
    }
}
