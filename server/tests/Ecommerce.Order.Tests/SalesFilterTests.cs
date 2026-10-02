using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMySales;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A seller's sales by the state of their own part (specs/131, #247) - what the seller's home counts as "to prepare",
/// in SQL, so the total covers every page rather than the window the overview reads.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class SalesFilterTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Each_state_is_counted_across_every_page_and_a_cancelled_part_or_order_is_never_to_prepare()
    {
        var seller = Guid.CreateVersion7();
        for (var i = 0; i < 3; i++)
            await SeedAsync(seller, OrderStatus.Paid, ShipmentStatus.Pending);
        await SeedAsync(seller, OrderStatus.Preparing, ShipmentStatus.Preparing);
        await SeedAsync(seller, OrderStatus.Shipped, ShipmentStatus.Shipped);
        await SeedAsync(seller, OrderStatus.Paid, ShipmentStatus.Pending, partCancelled: true);
        await SeedAsync(seller, OrderStatus.Cancelled, ShipmentStatus.Pending);
        // Another seller's part waiting on an order that also holds this seller's shipped part.
        await SeedAsync(seller, OrderStatus.Preparing, ShipmentStatus.Shipped, otherSellersPart: ShipmentStatus.Pending);

        // A page of one: the total is still every one of them, not the page.
        Assert.Equal(3, (await SalesAsync(seller, SalePartFilter.Paid, pageSize: 1)).TotalCount);
        Assert.Equal(1, (await SalesAsync(seller, SalePartFilter.Preparing)).TotalCount);
        Assert.Equal(2, (await SalesAsync(seller, SalePartFilter.Shipped)).TotalCount);
        Assert.Equal(2, (await SalesAsync(seller, SalePartFilter.Cancelled)).TotalCount);
        Assert.Equal(8, (await SalesAsync(seller, null)).TotalCount);
    }

    [Fact]
    public async Task An_unknown_state_is_refused()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => SalesAsync(Guid.CreateVersion7(), "Delivered"));
        Assert.Contains(refused.Errors, e => e.PropertyName == "Status");
    }

    private async Task SeedAsync(
        Guid seller, OrderStatus orderStatus, ShipmentStatus partStatus, bool partCancelled = false,
        ShipmentStatus? otherSellersPart = null)
    {
        var orderId = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var items = new List<OrderItem>
        {
            new() { Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), ProductName = "Camera", SellerId = seller, Quantity = 1, UnitPrice = 100_000 },
        };
        var parts = new List<OrderShipment>
        {
            new()
            {
                Id = Guid.CreateVersion7(), OrderId = orderId, SellerId = seller, Status = partStatus,
                CancelledAt = partCancelled ? now : null, CancelReason = partCancelled ? "Out of stock" : null,
            },
        };
        if (otherSellersPart is { } status)
        {
            items.Add(new OrderItem { Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), ProductName = "Lens", SellerId = other, Quantity = 1, UnitPrice = 50_000 });
            parts.Add(new OrderShipment { Id = Guid.CreateVersion7(), OrderId = orderId, SellerId = other, Status = status });
        }

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId, UserId = Guid.CreateVersion7(), TotalAmount = items.Sum(i => i.TotalPrice), Status = orderStatus,
            CreatedAt = now, UpdatedAt = now, Currency = "VND", Language = "vi", Items = items, Shipments = parts,
        });
        await context.SaveChangesAsync();
    }

    private async Task<PagedResponse<SaleSummaryResponse>> SalesAsync(Guid seller, string? status, int pageSize = 20)
    {
        _fixture.CurrentUser.Id = seller;
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMySalesQuery(1, pageSize, status));
    }
}
