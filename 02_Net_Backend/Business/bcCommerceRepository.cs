using System.Data;
using System.Globalization;
using Commerce.Api.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace Commerce.Api.Business;

/// <summary>Acceso a datos de commerce: toda la lógica de base de datos vive en stored procedures.</summary>
public class bcCommerceRepository
{
    private readonly string _connectionString;

    public bcCommerceRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CommerceDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'CommerceDb'.");
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Invoca sp_create_commerce con un lote (TVP). Devuelve las filas insertadas.</summary>
    internal async Task<int> InsertBatchAsync(
        SqlConnection connection, SqlTransaction transaction,
        IReadOnlyList<pvCommerceRow> rows, CancellationToken ct)
    {
        await using var command = new SqlCommand("dbo.sp_create_commerce", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120,
        };

        command.Parameters.Add(new SqlParameter("@items", SqlDbType.Structured)
        {
            TypeName = "dbo.commerce_type",
            Value = MeToSqlRecords(rows),
        });
        var inserted = new SqlParameter("@inserted", SqlDbType.Int) { Direction = ParameterDirection.Output };
        command.Parameters.Add(inserted);

        await command.ExecuteNonQueryAsync(ct);
        return Convert.ToInt32(inserted.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Invoca sp_process_commerce para un día.</summary>
    public async Task<DtoProcessCommerceResponse> ProcessAsync(DateOnly processDate, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new SqlCommand("dbo.sp_process_commerce", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 300,
        };

        command.Parameters.Add(new SqlParameter("@process_date", SqlDbType.Date)
        {
            Value = processDate.ToDateTime(TimeOnly.MinValue),
        });
        var evaluated = new SqlParameter("@evaluated", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var quarantined = new SqlParameter("@quarantined", SqlDbType.Int) { Direction = ParameterDirection.Output };
        command.Parameters.Add(evaluated);
        command.Parameters.Add(quarantined);

        await command.ExecuteNonQueryAsync(ct);

        return new DtoProcessCommerceResponse
        {
            ProcessDate = processDate,
            Evaluated = Convert.ToInt32(evaluated.Value, CultureInfo.InvariantCulture),
            Quarantined = Convert.ToInt32(quarantined.Value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Invoca sp_get_commerce_quarantine (paginado).</summary>
    public async Task<DtoQuarantinePage> GetQuarantineAsync(
        DateOnly? processDate, int pageNumber, int pageSize, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new SqlCommand("dbo.sp_get_commerce_quarantine", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60,
        };

        command.Parameters.Add(new SqlParameter("@process_date", SqlDbType.Date)
        {
            Value = processDate is null ? DBNull.Value : processDate.Value.ToDateTime(TimeOnly.MinValue),
        });
        command.Parameters.Add(new SqlParameter("@page_number", SqlDbType.Int) { Value = pageNumber });
        command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

        var page = new DtoQuarantinePage { PageNumber = pageNumber, PageSize = pageSize };

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // Orden de columnas de sp_get_commerce_quarantine
            page.Items.Add(new DtoQuarantineItem
            {
                Id = reader.GetInt64(0),
                PcNomcomred = reader.IsDBNull(1) ? null : reader.GetString(1),
                PcNumdoc = reader.IsDBNull(2) ? null : reader.GetString(2),
                PcProcessdate = DateOnly.FromDateTime(reader.GetDateTime(3)),
                Motivo = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
            });
            page.TotalCount = reader.GetInt32(6);
        }

        return page;
    }

    // TVP en streaming: se reutiliza un único SqlDataRecord, sin DataTable intermedio.
    private static IEnumerable<SqlDataRecord> MeToSqlRecords(IReadOnlyList<pvCommerceRow> rows)
    {
        var record = new SqlDataRecord(
            new SqlMetaData("pc_nomcomred", SqlDbType.NVarChar, 200),
            new SqlMetaData("pc_numdoc", SqlDbType.NVarChar, 50),
            new SqlMetaData("pc_processdate", SqlDbType.Date));

        foreach (var row in rows)
        {
            if (row.NomComRed is null) record.SetDBNull(0); else record.SetString(0, row.NomComRed);
            if (row.NumDoc is null) record.SetDBNull(1); else record.SetString(1, row.NumDoc);
            record.SetDateTime(2, row.ProcessDate.ToDateTime(TimeOnly.MinValue));
            yield return record;
        }
    }
}
