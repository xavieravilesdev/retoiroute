using System.ComponentModel.DataAnnotations;
using Commerce.Api.Business;
using Commerce.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

/// <summary>Carga, proceso y consulta de comercios.</summary>
[ApiController]
[Authorize]
[Route("api/commerce")]
[Produces("application/json")]
public class CtCommerceController(bcCommerceService commerceService, bcCsvReader csvReader) : ControllerBase
{
    private const long MaxUploadBytes = 20_000_000;

    /// <summary>Carga el archivo commerce_DDMMYYYY.csv en la tabla commerce.</summary>
    /// <remarks>
    /// El archivo no puede estar vacío, debe llamarse commerce_DDMMYYYY.csv y traer las columnas
    /// pc_nomcomred, pc_numdoc y pc_processdate. La carga es atómica: si una fila es inválida no se inserta nada.
    /// </remarks>
    /// <param name="file">Archivo CSV.</param>
    /// <response code="200">Archivo cargado.</response>
    /// <response code="400">Archivo vacío, con nombre o formato inválido.</response>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(DtoUploadCommerceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EpUploadCommerce(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return MeBadRequest("Archivo vacío", "El archivo no puede estar vacío.");

        // GetFileName evita rutas incluidas en el nombre enviado por el cliente.
        var fileName = Path.GetFileName(file.FileName);
        if (!csvReader.IsValidFileName(fileName))
            return MeBadRequest("Nombre de archivo inválido", "El archivo debe llamarse commerce_DDMMYYYY.csv con una fecha válida.");

        await using var stream = file.OpenReadStream();
        var result = await commerceService.UploadAsync(fileName, stream, ct);
        return Ok(result);
    }

    /// <summary>Valida los registros de un día y envía los inválidos a commerce_quarantine.</summary>
    /// <param name="request">Día a procesar (valor de pc_processdate).</param>
    /// <response code="200">Cantidad de registros evaluados y enviados a cuarentena.</response>
    /// <response code="404">No hay registros cargados para ese día.</response>
    [HttpPost("process")]
    [ProducesResponseType(typeof(DtoProcessCommerceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EpProcessCommerce([FromBody] DtoProcessCommerceRequest request, CancellationToken ct)
    {
        var result = await commerceService.ProcessAsync(request.ProcessDate, ct);

        return result.Evaluated == 0
            ? Problem(
                title: "Sin registros",
                detail: $"No hay registros cargados para la fecha {request.ProcessDate:yyyy-MM-dd}.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(result);
    }

    /// <summary>Lista los comercios de commerce_quarantine con el motivo del rechazo.</summary>
    /// <param name="processDate">Filtra por día (opcional).</param>
    /// <param name="pageNumber">Número de página (desde 1).</param>
    /// <param name="pageSize">Tamaño de página (1 a 1000).</param>
    [HttpGet("quarantine")]
    [ProducesResponseType(typeof(DtoQuarantinePage), StatusCodes.Status200OK)]
    public async Task<IActionResult> EpGetQuarantine(
        [FromQuery] DateOnly? processDate,
        [FromQuery, Range(1, int.MaxValue)] int pageNumber = 1,
        [FromQuery, Range(1, 1000)] int pageSize = 50,
        CancellationToken ct = default)
    {
        var page = await commerceService.GetQuarantineAsync(processDate, pageNumber, pageSize, ct);
        return Ok(page);
    }

    private ObjectResult MeBadRequest(string title, string detail)
        => Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
