namespace Azunt.BundleManagement;

public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, long totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }
    public long TotalCount { get; }
}
