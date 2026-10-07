namespace Commerce.Api.Dtos;

/// <summary>Registro de commerce_quarantine con el motivo del rechazo.</summary>
public class DtoQuarantineItem
{
    public long Id { get; set; }
    public string? PcNomcomred { get; set; }
    public string? PcNumdoc { get; set; }
    public DateOnly PcProcessdate { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
