namespace Ecommerce.Activity.Application.Common;

public record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount);
