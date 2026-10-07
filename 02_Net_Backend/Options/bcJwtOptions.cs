namespace Commerce.Api.Options;

/// <summary>Configuración de los tokens JWT (sección "Jwt").</summary>
public class bcJwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave simétrica (mínimo 32 bytes). Debe venir de user-secrets o variables de entorno.</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
