namespace Commerce.Api.Options;

/// <summary>Configuración de lectura del CSV (sección "Csv").</summary>
public class bcCsvOptions
{
    public const string SectionName = "Csv";

    public string Delimiter { get; set; } = ",";

    /// <summary>Formato de la columna pc_processdate.</summary>
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    /// <summary>Registros por llamada a sp_create_commerce.</summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>Lotes máximos en memoria entre el lector y el escritor (contrapresión).</summary>
    public int ChannelCapacity { get; set; } = 4;
}
