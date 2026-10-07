namespace Commerce.Api.Data;

/// <summary>Refresh token (tabla refresh_token). Solo se guarda el hash del token.</summary>
public class bcRefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bcAppUser User { get; set; } = null!;
}
