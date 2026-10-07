using System.ComponentModel.DataAnnotations;

namespace Commerce.Api.Dtos;

/// <summary>Refresh token emitido en el login (para renovar la sesión o cerrarla).</summary>
public class DtoRefreshRequest
{
    [Required, StringLength(512, MinimumLength = 1)]
    public required string RefreshToken { get; set; }
}
