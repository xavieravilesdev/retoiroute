namespace Commerce.Api.Dtos;

/// <summary>Página de registros en cuarentena.</summary>
public class DtoQuarantinePage
{
    public List<DtoQuarantineItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
