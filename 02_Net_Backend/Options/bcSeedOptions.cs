namespace Commerce.Api.Options;

/// <summary>Usuario administrador inicial (sección "Seed"). Si la contraseña está vacía no se crea.</summary>
public class bcSeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = string.Empty;
}
