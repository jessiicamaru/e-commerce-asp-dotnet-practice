using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Ecommerce.Order.Application.Orders.Queries.GetMySales;
using Ecommerce.Order.Application.Orders.Queries.GetOrdersForFulfilment;
using Ecommerce.Order.Application.Orders.Queries.GetOrdersForStaff;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What is in an order, on the lists (specs/132, #248): up to three lines by their frozen name, the biggest first - and
/// on a seller's sale, only that seller's own.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class OrderPreviewTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task A_customer_list_names_at_most_three_lines_the_biggest_first()
    {
        var customer = Guid.CreateVersion7();
        await SeedAsync(customer, null, ["Strap", "Canon EOS R50", "Battery", "RF 50mm"], prices: [200_000, 18_500_000, 900_000, 5_000_000]);

        _fixture.CurrentUser.Id = customer;
        var row = Assert.Single((await SendAsync(new GetMyOrdersQuery())).Items);

        Assert.Equal(["Canon EOS R50", "RF 50mm", "Battery"], row.Lines!.Select(l => l.ProductName));
        Assert.Equal(4, row.ItemCount);
    }

    [Fact]
    public async Task A_sellers_sale_names_only_their_own_lines()
    {
        var customer = Guid.CreateVersion7();
        var seller = Guid.CreateVersion7();
        // The other seller's lens is the biggest line on the order - and still not hers to see.
        await SeedAsync(customer, seller, ["Someone else's lens", "Her camera", "Her strap", "Her battery", "Her bag"],
            mine: [1, 2, 3, 4], prices: [30_000_000, 18_500_000, 100_000, 900_000, 400_000]);

        _fixture.CurrentUser.Id = seller;
        var row = Assert.Single((await SendAsync(new GetMySalesQuery())).Items);

        Assert.Equal(["Her camera", "Her battery", "Her bag"], row.Lines!.Select(l => l.ProductName));
    }

    [Fact]
    public async Task The_staff_search_names_the_lines_too()
    {
        var customer = Guid.CreateVersion7();
        var orderId = await SeedAsync(customer, null, ["Strap", "Canon EOS R50", "Battery", "RF 50mm"], prices: [200_000, 18_500_000, 900_000, 5_000_000]);

        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.CurrentUser.Roles.Add("Admin");
        try
        {
            var row = Assert.Single((await SendAsync(new GetOrdersForStaffQuery(CustomerId: customer))).Items);
            Assert.Equal(orderId, row.OrderId);
            Assert.Equal(["Canon EOS R50", "RF 50mm", "Battery"], row.Lines!.Select(l => l.ProductName));
            Assert.All(row.Lines!, line => Assert.NotNull(line.VariantId));
        }
        finally
        {
            _fixture.CurrentUser.Roles.Remove("Admin");
        }
    }

    [Fact]
    public async Task The_fulfilment_queue_names_the_biggest_three_lines()
    {
        var customer = Guid.CreateVersion7();
        var orderId = await SeedAsync(customer, null, ["Strap", "Canon EOS R50", "Battery", "RF 50mm"], prices: [200_000, 18_500_000, 900_000, 5_000_000]);

        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.CurrentUser.Roles.Add("Admin");
        try
        {
            // The oldest first, a page at a time: walk until this order is found.
            for (var page = 1; ; page++)
            {
                var result = await SendAsync(new GetOrdersForFulfilmentQuery("Paid", page, 100));
                var row = result.Items.FirstOrDefault(r => r.OrderId == orderId);
                if (row is not null)
                {
                    Assert.Equal(["Canon EOS R50", "RF 50mm", "Battery"], row.Lines!.Select(l => l.ProductName));
                    return;
                }
                Assert.True(result.Items.Count == 100, "the order is in the Paid queue");
            }
        }
        finally
        {
            _fixture.CurrentUser.Roles.Remove("Admin");
        }
    }

    /// <summary>A paid order of these lines; <paramref name="mine"/> are the seller's.</summary>
    private async Task<Guid> SeedAsync(Guid customer, Guid? seller, string[] names, int[]? mine = null, decimal[]? prices = null)
    {
        var orderId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var items = names.Select((name, index) => new OrderItem
        {
            Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), VariantId = Guid.CreateVersion7(),
            ProductName = name, Quantity = 1, UnitPrice = prices?[index] ?? 100_000,
            SellerId = seller is { } s ? (mine is null || mine.Contains(index) ? s : Guid.CreateVersion7()) : null,
        }).ToList();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId, UserId = customer, TotalAmount = items.Sum(i => i.TotalPrice), Status = OrderStatus.Paid,
            CreatedAt = now, UpdatedAt = now, Currency = "VND", Language = "en", Items = items,
        });
        await context.SaveChangesAsync();
        return orderId;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
