namespace Commerce.Api.Data;

/// <summary>Usuario de la aplicación (tabla app_user).</summary>
public class bcAppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public List<bcRefreshToken> RefreshTokens { get; set; } = [];
}
