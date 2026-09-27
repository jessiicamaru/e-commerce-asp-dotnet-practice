using Ecommerce.Order.Application.Orders.Queries.GetOrdersForStaff;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Staff find any order (#194, specs/096): by the start of its id, by its customer, in any status - where the fulfilment
/// queue shows only paid orders by the shop's parcel.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class StaffOrderSearchTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task An_order_is_found_by_the_start_of_its_id_in_any_status()
    {
        var failed = await OrderAsync(Guid.CreateVersion7(), OrderStatus.Failed);
        await OrderAsync(Guid.CreateVersion7(), OrderStatus.Paid);

        var page = await SendAsync(new GetOrdersForStaffQuery(Search: failed.ToString()[..13].ToUpperInvariant()));

        var found = Assert.Single(page.Items);
        Assert.Equal((failed, "Failed"), (found.OrderId, found.Status));
    }

    [Fact]
    public async Task A_customers_orders_are_listed_newest_first_and_nobody_elses()
    {
        var customer = Guid.CreateVersion7();
        var older = await OrderAsync(customer, OrderStatus.Cancelled, minutesAgo: 30);
        var newer = await OrderAsync(customer, OrderStatus.Submitted, minutesAgo: 10);
        await OrderAsync(Guid.CreateVersion7(), OrderStatus.Paid);

        var page = await SendAsync(new GetOrdersForStaffQuery(CustomerId: customer));

        Assert.Equal([newer, older], page.Items.Select(o => o.OrderId));
        Assert.All(page.Items, o => Assert.Equal(customer, o.UserId));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task A_status_narrows_it_and_Paid_includes_the_legacy_Completed()
    {
        var customer = Guid.CreateVersion7();
        var paid = await OrderAsync(customer, OrderStatus.Paid, minutesAgo: 20);
        var completed = await OrderAsync(customer, OrderStatus.Completed, minutesAgo: 10);
        await OrderAsync(customer, OrderStatus.Failed);

        var page = await SendAsync(new GetOrdersForStaffQuery(Status: "Paid", CustomerId: customer));

        Assert.Equal([completed, paid], page.Items.Select(o => o.OrderId));
        Assert.All(page.Items, o => Assert.Equal("Paid", o.Status));
    }

    [Theory]
    [InlineData("Pending", null)]            // never reachable - not a status staff look for
    [InlineData("Delivered", null)]
    [InlineData(null, "01a")]                // too short to be a search
    [InlineData(null, "01a0zz2b")]           // not hexadecimal
    public async Task A_status_or_search_that_is_not_one_is_refused(string? status, string? search)
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetOrdersForStaffQuery(status, search)));
    }

    [Fact]
    public async Task A_page_holds_at_most_fifty()
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetOrdersForStaffQuery(PageSize: 51)));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> OrderAsync(Guid customer, OrderStatus status, int minutesAgo = 1)
    {
        var id = Guid.CreateVersion7();
        var at = DateTime.UtcNow.AddMinutes(-minutesAgo);
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        context.Orders.Add(new Domain.Entities.Order
        {
            Id = id,
            UserId = customer,
            TotalAmount = 20m,
            Status = status,
            CreatedAt = at,
            UpdatedAt = at,
            Items =
            [
                new OrderItem { Id = Guid.CreateVersion7(), OrderId = id, ProductId = Guid.CreateVersion7(), ProductName = "Lamp", Quantity = 1, UnitPrice = 20m },
            ],
        });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
