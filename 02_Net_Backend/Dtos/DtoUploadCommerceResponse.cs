namespace Commerce.Api.Dtos;

/// <summary>Resultado de la carga del archivo CSV.</summary>
public class DtoUploadCommerceResponse
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>Registros insertados en la tabla commerce.</summary>
    public int Inserted { get; set; }

    /// <summary>Cantidad de llamadas (lotes) a sp_create_commerce.</summary>
    public int Batches { get; set; }

    /// <summary>Fechas distintas encontradas en pc_processdate (para elegir qué día procesar).</summary>
    public List<DateOnly> ProcessDates { get; set; } = [];
}
