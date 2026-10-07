using System.ComponentModel.DataAnnotations;

namespace Commerce.Api.Dtos;

/// <summary>Credenciales de acceso.</summary>
public class DtoLoginRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Username { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Password { get; set; }
}
