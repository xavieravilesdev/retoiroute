namespace Commerce.Api.Dtos;

/// <summary>Resultado de un login o de una renovación de token.</summary>
public class DtoAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Vencimiento del access token (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
