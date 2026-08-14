namespace Ledgerly.Shared;

public sealed record PagedRequest(int Page = 1, int PageSize = 20)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize <= 0 ? 20 : Math.Min(PageSize, 100);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);