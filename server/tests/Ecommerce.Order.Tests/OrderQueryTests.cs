using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrders;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Who can read which order.
/// <para>
/// These tests set <see cref="OrderTestFixture.CurrentUser"/> directly, so what they establish is
/// that the owner filter is applied to whatever identity the handler is given. That the identity is
/// the one carried by a valid access token is a different claim, and not one these tests make.
/// </para>
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class OrderQueryTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task A_shopper_sees_their_own_orders_newest_first_and_nobody_elses()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();

        var older = await SeedAsync(alice, DateTime.UtcNow.AddHours(-2));
        var newer = await SeedAsync(alice, DateTime.UtcNow.AddHours(-1));
        var bobs = await SeedAsync(bob, DateTime.UtcNow);

        _fixture.CurrentUser.Id = alice;
        var page = await SendAsync(new GetMyOrdersQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer, older], page.Items.Select(x => x.OrderId));
        Assert.DoesNotContain(bobs, page.Items.Select(x => x.OrderId));
    }

    [Fact]
    public async Task Another_shoppers_order_is_not_found_rather_than_forbidden()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();

        var bobsOrder = await SeedAsync(bob, DateTime.UtcNow);

        _fixture.CurrentUser.Id = alice;

        // The exception type is the assertion. A ForbiddenException here would still "reject the
        // request" and still look like a passing test, while telling the caller that the id is
        // real — which is the disclosure this rule exists to prevent.
        var real = await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new GetMyOrderByIdQuery(bobsOrder)));

        var imaginary = await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new GetMyOrderByIdQuery(Guid.CreateVersion7())));

        // Indistinguishable from outside, which is the point.
        Assert.Equal(imaginary.Message, real.Message);
    }

    [Fact]
    public async Task A_shopper_can_open_their_own_order_with_its_items()
    {
        var alice = Guid.CreateVersion7();
        var orderId = await SeedAsync(alice, DateTime.UtcNow);

        _fixture.CurrentUser.Id = alice;
        var order = await SendAsync(new GetMyOrderByIdQuery(orderId));

        Assert.Equal(orderId, order.OrderId);
        Assert.Equal(alice, order.UserId);
        Assert.Equal(nameof(OrderStatus.Submitted), order.Status);
        Assert.Single(order.Items);
        Assert.Equal(129.99m, order.Items[0].TotalPrice);
    }

    [Fact]
    public async Task Paging_reports_an_honest_total_and_a_page_past_the_end_is_empty()
    {
        var alice = Guid.CreateVersion7();

        for (var i = 0; i < 3; i++)
        {
            await SeedAsync(alice, DateTime.UtcNow.AddMinutes(-i));
        }

        _fixture.CurrentUser.Id = alice;

        var second = await SendAsync(new GetMyOrdersQuery(Page: 2, PageSize: 1));
        Assert.Single(second.Items);
        Assert.Equal(3, second.TotalCount);

        // An empty page is a correct answer, not an error — and the total still tells the caller
        // they overshot.
        var beyond = await SendAsync(new GetMyOrdersQuery(Page: 99, PageSize: 20));
        Assert.Empty(beyond.Items);
        Assert.Equal(3, beyond.TotalCount);
    }

    [Fact]
    public async Task The_list_reports_how_many_items_an_order_has_without_returning_them()
    {
        var alice = Guid.CreateVersion7();
        await SeedAsync(alice, DateTime.UtcNow, itemCount: 3);

        _fixture.CurrentUser.Id = alice;
        var page = await SendAsync(new GetMyOrdersQuery());

        Assert.Equal(3, page.Items.Single().ItemCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 5000)]
    public async Task Out_of_range_paging_is_rejected_rather_than_clamped(int page, int pageSize)
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();

        await Assert.ThrowsAsync<ValidationException>(
            () => SendAsync(new GetMyOrdersQuery(page, pageSize)));
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> SeedAsync(Guid userId, DateTime createdAt, int itemCount = 1)
    {
        var orderId = Guid.CreateVersion7();

        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        context.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId,
            UserId = userId,
            TotalAmount = 129.99m * itemCount,
            Status = OrderStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Items = Enumerable.Range(0, itemCount).Select(i => new OrderItem
            {
                Id = Guid.CreateVersion7(),
                OrderId = orderId,
                ProductId = Guid.CreateVersion7(),
                ProductName = $"Product {i}",
                Quantity = 1,
                UnitPrice = 129.99m
            }).ToList()
        });

        await context.SaveChangesAsync();

        return orderId;
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
