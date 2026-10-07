using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Business;

/// <summary>
/// Manejo central de errores: errores de formato del CSV -> 400, resto -> 500 sin detalles internos (OWASP).
/// </summary>
public sealed class bcGlobalExceptionHandler(ILogger<bcGlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // El cliente cerró la conexión: no hay a quién responder.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return true;

        var (status, title, detail) = exception switch
        {
            bcCsvFormatException => (StatusCodes.Status400BadRequest, "Archivo CSV inválido", exception.Message),
            BadHttpRequestException bad => (bad.StatusCode, "Solicitud inválida", "La solicitud no es válida o excede el tamaño permitido."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado. Intente nuevamente."),
        };

        if (status >= 500)
            logger.LogError(exception, "Error no controlado");
        else
            logger.LogWarning("Solicitud rechazada ({Status}): {Title}", status, title);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = detail }, ct);
        return true;
    }
}
