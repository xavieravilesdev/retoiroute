using System.Threading.Channels;
using Commerce.Api.Dtos;
using Commerce.Api.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Business;

/// <summary>
/// Orquesta la carga del CSV como un pipeline productor/consumidor:
/// un productor lee y valida el archivo en streaming y un consumidor inserta los lotes
/// dentro de una única transacción. El canal acotado aplica contrapresión, por lo que
/// el uso de memoria queda limitado a unos pocos lotes sin importar el tamaño del archivo.
/// </summary>
public class bcCommerceService(
    bcCsvReader csvReader,
    bcCommerceRepository repository,
    IOptions<bcCsvOptions> options,
    ILogger<bcCommerceService> logger)
{
    private readonly bcCsvOptions _options = options.Value;

    /// <summary>Carga el CSV completo de forma atómica: o entra todo, o no entra nada.</summary>
    public async Task<DtoUploadCommerceResponse> UploadAsync(string fileName, Stream content, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var processDates = new SortedSet<DateOnly>();

        var channel = Channel.CreateBounded<IReadOnlyList<pvCommerceRow>>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait,
            });

        var producer = Task.Run(() => MeProduceAsync(content, processDates, channel.Writer, linked.Token), linked.Token);

        await using var connection = await repository.OpenConnectionAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        var inserted = 0;
        var batches = 0;
        try
        {
            // Si el productor falla, su excepción sale de ReadAllAsync y la transacción se revierte al liberarse.
            await foreach (var batch in channel.Reader.ReadAllAsync(linked.Token))
            {
                inserted += await repository.InsertBatchAsync(connection, transaction, batch, linked.Token);
                batches++;
            }

            await producer;
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await linked.CancelAsync();
            throw;
        }
        finally
        {
            await MeAwaitQuietlyAsync(producer);
        }

        logger.LogInformation("Carga completada: {Rows} registros en {Batches} lotes", inserted, batches);

        return new DtoUploadCommerceResponse
        {
            FileName = fileName,
            Inserted = inserted,
            Batches = batches,
            ProcessDates = [.. processDates],
        };
    }

    /// <summary>Valida y mueve a cuarentena los registros de un día.</summary>
    public async Task<DtoProcessCommerceResponse> ProcessAsync(DateOnly processDate, CancellationToken ct)
    {
        var result = await repository.ProcessAsync(processDate, ct);
        logger.LogInformation("Proceso del día {ProcessDate}: {Evaluated} evaluados, {Quarantined} en cuarentena",
            processDate, result.Evaluated, result.Quarantined);
        return result;
    }

    /// <summary>Consulta paginada de commerce_quarantine.</summary>
    public Task<DtoQuarantinePage> GetQuarantineAsync(DateOnly? processDate, int pageNumber, int pageSize, CancellationToken ct)
        => repository.GetQuarantineAsync(processDate, pageNumber, pageSize, ct);

    private async Task MeProduceAsync(
        Stream content, ISet<DateOnly> processDates,
        ChannelWriter<IReadOnlyList<pvCommerceRow>> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var batch in csvReader.EnumerateBatchesAsync(content, processDates, ct))
                await writer.WriteAsync(batch, ct);

            writer.Complete();
        }
        catch (Exception ex)
        {
            // La excepción viaja por el canal hasta el consumidor.
            writer.Complete(ex);
        }
    }

    private static async Task MeAwaitQuietlyAsync(Task task)
    {
        try { await task; }
        catch { /* ya reportado al consumidor a través del canal */ }
    }
}
