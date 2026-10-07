namespace Commerce.Api.Business;

/// <summary>Fila ya validada del CSV, lista para enviarse a SQL Server.</summary>
internal sealed record pvCommerceRow(string? NomComRed, string? NumDoc, DateOnly ProcessDate);
