namespace Ecommerce.Order.Domain.Enums;

public enum OrderStatus
{
    Pending = 1,
    Submitted = 2,
    StockReserved = 3,
    Paid = 4,
    Completed = 5,
    Cancelled = 6,
    Failed = 7
}
