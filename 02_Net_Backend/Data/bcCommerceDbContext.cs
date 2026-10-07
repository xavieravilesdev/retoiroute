using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Data;

/// <summary>
/// Contexto EF Core. Solo cubre la autenticación; la carga y el proceso del CSV
/// pasan por stored procedures (ver bcCommerceRepository).
/// </summary>
public class bcCommerceDbContext(DbContextOptions<bcCommerceDbContext> options) : DbContext(options)
{
    public DbSet<bcAppUser> Users => Set<bcAppUser>();
    public DbSet<bcRefreshToken> RefreshTokens => Set<bcRefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<bcAppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2(0)");
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<bcRefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2(0)");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("datetime2(0)");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });
    }
}
