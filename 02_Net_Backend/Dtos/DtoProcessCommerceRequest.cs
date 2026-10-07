namespace Commerce.Api.Dtos;

/// <summary>Día a procesar (valor de la columna pc_processdate).</summary>
public class DtoProcessCommerceRequest
{
    public required DateOnly ProcessDate { get; set; }
}
