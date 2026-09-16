namespace Ecommerce.Payment.Application.Common.Models;

public class PaginatedList<T>(List<T> items, int totalCount, int pageNumber, int pageSize)
{
    public List<T> Items { get; } = items;
    public int TotalCount { get; } = totalCount;
    public int PageNumber { get; } = pageNumber;
    public int PageSize { get; } = pageSize;
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
