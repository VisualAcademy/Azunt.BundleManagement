namespace Azunt.BundleManagement;

public sealed class BundleFilterOptions
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
    public string SearchQuery { get; set; } = string.Empty;
    public string SortOrder { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool ActiveOnly { get; set; }
}
