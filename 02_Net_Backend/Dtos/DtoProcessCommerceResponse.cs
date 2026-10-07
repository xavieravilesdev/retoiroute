namespace Commerce.Api.Dtos;

/// <summary>Resultado del proceso de un día.</summary>
public class DtoProcessCommerceResponse
{
    public DateOnly ProcessDate { get; set; }

    /// <summary>Registros del día evaluados.</summary>
    public int Evaluated { get; set; }

    /// <summary>Registros enviados a commerce_quarantine.</summary>
    public int Quarantined { get; set; }
}
