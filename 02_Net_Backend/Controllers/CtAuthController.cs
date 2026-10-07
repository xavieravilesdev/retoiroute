using System.Security.Claims;
using Commerce.Api.Dtos;
using Commerce.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Commerce.Api.Controllers;

/// <summary>Autenticación con JWT y refresh tokens.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class CtAuthController(bcAuthService authService) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve un access token y un refresh token.</summary>
    /// <param name="request">Usuario y contraseña.</param>
    /// <response code="200">Credenciales válidas.</response>
    /// <response code="401">Credenciales inválidas.</response>
    /// <response code="429">Demasiados intentos.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(DtoAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EpLogin([FromBody] DtoLoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return result is null ? MeUnauthorized("Credenciales inválidas") : Ok(result);
    }

    /// <summary>Renueva la sesión: canjea un refresh token por un nuevo par de tokens.</summary>
    /// <param name="request">Refresh token vigente.</param>
    /// <response code="200">Nuevos tokens.</response>
    /// <response code="401">Refresh token inválido, vencido o revocado.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(DtoAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EpRefreshToken([FromBody] DtoRefreshRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, ct);
        return result is null ? MeUnauthorized("Sesión inválida o vencida") : Ok(result);
    }

    /// <summary>Cierra la sesión revocando el refresh token.</summary>
    /// <param name="request">Refresh token a revocar.</param>
    /// <response code="204">Sesión cerrada.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EpLogout([FromBody] DtoRefreshRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            return Unauthorized();

        await authService.LogoutAsync(userId, request.RefreshToken, ct);
        return NoContent();
    }

    private ObjectResult MeUnauthorized(string title)
        => Problem(title: title, statusCode: StatusCodes.Status401Unauthorized);
}
