using Ecommerce.Cart.Application.Carts;
using Ecommerce.Cart.Application.Carts.Commands.AddToCart;
using Ecommerce.Cart.Application.Carts.Queries.GetMyCart;
using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Tests;

/// <summary>
/// A cart before signing in (specs/162, #370): the browser's lines priced by the code that prices a stored cart, and
/// merged into the account's cart on sign-in - the larger quantity per shape, so a repeat changes nothing.
/// </summary>
[Collection(nameof(CartTestCollection))]
public class GuestCartTests(CartTestFixture fixture)
{
    private readonly CartTestFixture _fixture = fixture;

    private static readonly Guid Camera = Guid.CreateVersion7();
    private static readonly Guid Black = Guid.CreateVersion7();
    private static readonly Guid Silver = Guid.CreateVersion7();
    private static readonly Guid Withdrawn = Guid.CreateVersion7();
    private static readonly Guid Gone = Guid.CreateVersion7();

    /// <summary>What Catalog says: two shapes on sale, one withdrawn, one deleted.</summary>
    private sealed class Catalogue : ICatalogProducts
    {
        public Task<CatalogDescription> DescribeAsync(
            IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default, string language = "", string currency = "")
        {
            var known = new List<CatalogProduct>
            {
                new(Camera, "Máy ảnh", 1_000_000m, true, Black, "Màu: Đen"),
                new(Camera, "Máy ảnh", 1_200_000m, true, Silver, "Màu: Bạc"),
                new(Camera, "Máy ảnh", 900_000m, false, Withdrawn, "Màu: Đỏ"),
            };
            return Task.FromResult(new CatalogDescription(
                true,
                known.Where(p => variantIds.Contains(p.VariantId)).ToList(),
                variantIds.Where(id => known.All(p => p.VariantId != id)).ToList()));
        }
    }

    [Fact]
    public async Task The_browsers_lines_are_priced_as_the_same_lines_in_a_stored_cart_and_nothing_is_stored()
    {
        var customer = Guid.CreateVersion7();
        List<CartLineInput> lines =
        [
            new(Camera, Black, 2),
            new(Camera, Withdrawn, 1),
            new(Camera, Gone, 1),
        ];

        var guest = await SendAsync(Guid.CreateVersion7(), new PriceCartLinesQuery(lines));

        foreach (var line in lines)
        {
            await SendAsync(customer, new AddToCartCommand(line.ProductId, line.Quantity, line.VariantId));
        }

        var stored = await SendAsync(customer, new GetMyCartQuery());

        // The same lines, statuses, estimate and checkout answer, line for line.
        Assert.Equal(stored.Lines, guest.Lines);
        Assert.Equal(stored.EstimatedTotal, guest.EstimatedTotal);
        Assert.Equal(stored.CanCheckOut, guest.CanCheckOut);
        Assert.Equal(
            [CartLineStatus.Available, CartLineStatus.NotForSale, CartLineStatus.NoLongerAvailable],
            guest.Lines.Select(l => l.Status));
        Assert.Equal(2_000_000m, guest.EstimatedTotal);
        Assert.False(guest.CanCheckOut);

        // Pricing wrote nothing: only the customer who added has a cart.
        Assert.Equal(1, await CartsAsync([customer, Guid.Empty]));
    }

    [Fact]
    public async Task The_same_shape_sent_twice_is_one_line()
    {
        var priced = await SendAsync(Guid.CreateVersion7(), new PriceCartLinesQuery([new(Camera, Black, 1), new(Camera, Black, 3)]));

        var line = Assert.Single(priced.Lines);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task A_merge_keeps_every_line_and_the_larger_quantity_and_a_repeat_changes_nothing()
    {
        var customer = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await SendAsync(customer, new AddToCartCommand(Camera, 1, Black));
        await SendAsync(customer, new AddToCartCommand(Camera, 3, Silver));
        await SendAsync(customer, new AddToCartCommand(other, 1, other));

        MergeCartCommand merge = new([new(Camera, Black, 2), new(Camera, Silver, 1), new(Camera, Withdrawn, 1)]);
        await SendAsync(customer, merge);
        var once = await QuantitiesAsync(customer);

        // Black raised to 2, Silver kept at 3 (never lowered), the other line kept, Withdrawn added.
        Assert.Equal(2, once[Black]);
        Assert.Equal(3, once[Silver]);
        Assert.Equal(1, once[other]);
        Assert.Equal(1, once[Withdrawn]);

        await SendAsync(customer, merge);
        Assert.Equal(once, await QuantitiesAsync(customer));
    }

    [Fact]
    public async Task A_merge_into_an_account_with_no_cart_yet_creates_it()
    {
        var customer = Guid.CreateVersion7();

        await SendAsync(customer, new MergeCartCommand([new(Camera, Black, 2)]));

        Assert.Equal(2, (await QuantitiesAsync(customer))[Black]);
    }

    [Theory]
    [InlineData(51, 1)]     // more lines than a cart holds
    [InlineData(1, 0)]      // nothing of it
    [InlineData(1, 1000)]   // more than anybody buys at once
    public async Task Too_many_lines_or_a_quantity_out_of_range_is_refused_by_both(int count, int quantity)
    {
        var lines = Enumerable.Range(0, count).Select(_ => new CartLineInput(Guid.CreateVersion7(), Guid.CreateVersion7(), quantity)).ToList();
        var customer = Guid.CreateVersion7();

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(customer, new PriceCartLinesQuery(lines)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(customer, new MergeCartCommand(lines)));
        Assert.Equal(0, await CartsAsync([customer]));
    }

    private async Task<int> CartsAsync(Guid[] users)
    {
        await using var scope = _fixture.For(Guid.NewGuid()).CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        return await context.Carts.CountAsync(c => users.Contains(c.UserId));
    }

    private async Task<Dictionary<Guid, int>> QuantitiesAsync(Guid customer)
    {
        await using var scope = _fixture.For(customer).CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        return await context.Carts.Where(c => c.UserId == customer)
            .SelectMany(c => c.Lines)
            .ToDictionaryAsync(l => l.VariantId ?? l.ProductId, l => l.Quantity);
    }

    private async Task<T> SendAsync<T>(Guid user, IRequest<T> request)
    {
        await using var provider = _fixture.For(user, new Catalogue());
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(Guid user, IRequest request)
    {
        await using var provider = _fixture.For(user, new Catalogue());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
