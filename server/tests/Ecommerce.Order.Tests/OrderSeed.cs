using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

internal static class OrderSeed
{
    /// <summary>An order already in <paramref name="status"/>, backdated so "UpdatedAt moved" means something.</summary>
    public static async Task<Guid> OrderInAsync(OrderTestFixture fixture, OrderStatus status, string? tracking = null)
    {
        var orderId = Guid.CreateVersion7();
        var at = DateTime.UtcNow.AddMinutes(-5);

        await using var scope = fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId,
            UserId = Guid.CreateVersion7(),
            TotalAmount = 20m,
            Status = status,
            TrackingReference = tracking,
            CreatedAt = at,
            UpdatedAt = at,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.CreateVersion7(),
                    OrderId = orderId,
                    ProductId = Guid.CreateVersion7(),
                    ProductName = "Desk Lamp",
                    Quantity = 1,
                    UnitPrice = 15m
                }
            ]
        });

        await context.SaveChangesAsync();
        return orderId;
    }

    public static async Task<Domain.Entities.Order> ReadAsync(OrderTestFixture fixture, Guid orderId)
    {
        await using var scope = fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        return await context.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
    }
}
