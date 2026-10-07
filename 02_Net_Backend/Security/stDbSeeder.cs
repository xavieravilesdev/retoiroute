using Commerce.Api.Data;
using Commerce.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Security;

/// <summary>Crea el usuario administrador inicial si todavía no existe ningún usuario.</summary>
public static class stDbSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(stDbSeeder));
        var seed = scope.ServiceProvider.GetRequiredService<IOptions<bcSeedOptions>>().Value;

        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            logger.LogWarning("Seed:AdminPassword vacío: no se crea el usuario inicial.");
            return;
        }

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<bcCommerceDbContext>();
            if (await db.Users.AnyAsync(ct)) return;

            db.Users.Add(new bcAppUser
            {
                Username = seed.AdminUsername,
                PasswordHash = stPasswordHasher.Hash(seed.AdminPassword),
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Usuario inicial '{User}' creado.", seed.AdminUsername);
        }
        catch (Exception ex)
        {
            // La API debe poder arrancar aunque la base todavía no esté disponible.
            logger.LogError(ex, "No se pudo crear el usuario inicial. ¿Se ejecutó 01_database.sql?");
        }
    }
}
