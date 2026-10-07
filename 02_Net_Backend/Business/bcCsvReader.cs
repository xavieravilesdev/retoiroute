using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Commerce.Api.Options;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Business;

/// <summary>
/// Lee el CSV de comercios en streaming (sin cargarlo completo en memoria),
/// valida el formato y lo entrega en lotes.
/// </summary>
public class bcCsvReader(IOptions<bcCsvOptions> options)
{
    private const int MaxNomComRedLength = 200;
    private const int MaxNumDocLength = 50;
    private const string ColNomComRed = "pc_nomcomred";
    private const string ColNumDoc = "pc_numdoc";
    private const string ColProcessDate = "pc_processdate";

    // Timeout para evitar ReDoS con nombres de archivo maliciosos.
    private static readonly Regex FileNameRegex = new(
        @"^commerce_(\d{8})\.csv$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private readonly bcCsvOptions _options = options.Value;

    /// <summary>Valida el patrón commerce_DDMMYYYY.csv (incluida una fecha real).</summary>
    public bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var match = FileNameRegex.Match(fileName);
        return match.Success
               && DateOnly.TryParseExact(match.Groups[1].Value, "ddMMyyyy",
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    /// <summary>
    /// Recorre el CSV y devuelve lotes de hasta BatchSize filas.
    /// Lanza <see cref="bcCsvFormatException"/> ante cualquier problema de formato.
    /// </summary>
    /// <param name="stream">Contenido del archivo.</param>
    /// <param name="processDates">Se completa con las fechas distintas encontradas.</param>
    internal async IAsyncEnumerable<IReadOnlyList<pvCommerceRow>> EnumerateBatchesAsync(
        Stream stream,
        ISet<DateOnly> processDates,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 81920, leaveOpen: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _options.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
        };
        using var csv = new CsvReader(reader, config);

        if (!await MeReadRowAsync(csv))
            throw new bcCsvFormatException("El archivo CSV está vacío.");

        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? [])
            .Select(h => h.Trim().ToLowerInvariant())
            .ToArray();

        var iNom = Array.IndexOf(headers, ColNomComRed);
        var iDoc = Array.IndexOf(headers, ColNumDoc);
        var iDate = Array.IndexOf(headers, ColProcessDate);
        if (iNom < 0 || iDoc < 0 || iDate < 0)
            throw new bcCsvFormatException(
                $"El encabezado debe contener las columnas {ColNomComRed}, {ColNumDoc} y {ColProcessDate}.");

        var batch = new List<pvCommerceRow>(_options.BatchSize);
        var dataRows = 0;

        while (await MeReadRowAsync(csv))
        {
            ct.ThrowIfCancellationRequested();

            var row = csv.Parser.Row;
            var nom = (csv.GetField(iNom) ?? string.Empty).Trim();
            var doc = (csv.GetField(iDoc) ?? string.Empty).Trim();
            var rawDate = (csv.GetField(iDate) ?? string.Empty).Trim();

            if (nom.Length > MaxNomComRedLength || doc.Length > MaxNumDocLength)
                throw new bcCsvFormatException(
                    $"La fila {row} excede la longitud máxima permitida " +
                    $"({MaxNomComRedLength} para {ColNomComRed}, {MaxNumDocLength} para {ColNumDoc}).");

            // pc_processdate es obligatoria y NOT NULL en la tabla: una fecha inválida rechaza el archivo.
            if (!DateOnly.TryParseExact(rawDate, _options.DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                throw new bcCsvFormatException(
                    $"La fila {row} tiene una fecha {ColProcessDate} inválida (formato esperado: {_options.DateFormat}).");

            processDates.Add(date);
            batch.Add(new pvCommerceRow(nom, doc, date));
            dataRows++;

            if (batch.Count >= _options.BatchSize)
            {
                yield return batch;
                batch = new List<pvCommerceRow>(_options.BatchSize);
            }
        }

        if (dataRows == 0)
            throw new bcCsvFormatException("El archivo CSV no contiene registros de datos.");

        if (batch.Count > 0)
            yield return batch;
    }

    // yield no puede estar dentro de un try/catch, por eso la lectura va en un método aparte.
    private static async Task<bool> MeReadRowAsync(CsvReader csv)
    {
        try
        {
            return await csv.ReadAsync();
        }
        catch (CsvHelperException ex)
        {
            throw new bcCsvFormatException(
                $"El archivo no tiene un formato CSV válido (fila {csv.Parser.Row}).", ex);
        }
    }
}
