using System.Text.Json;
using Ecommerce.Cart.Application.MyData;
using Ecommerce.Cart.Domain.Entities;
using Ecommerce.Cart.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Tests;

/// <summary>What Cart holds about a person, handed to them (#217, specs/111): their cart's lines, nobody else's.</summary>
[Collection(nameof(CartTestCollection))]
public class MyDataTests(CartTestFixture fixture)
{
    private readonly CartTestFixture _fixture = fixture;

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<CartDbContext>().Model;

        Assert.Empty(CartPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    [Fact]
    public async Task A_person_gets_their_own_cart_and_nobody_else_s()
    {
        var (mai, maiVariant) = await PersonWithACartAsync();
        var (_, lanVariant) = await PersonWithACartAsync();

        await using var provider = _fixture.For(mai);
        await using var scope = provider.CreateAsyncScope();
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyDataQuery());

        Assert.Equal("cart", export.Service);
        Assert.Single(export.Sections["cart"]);
        Assert.Contains(export.Withheld, w => w.Table == "checkout_outcomes");
        var json = JsonSerializer.Serialize(export);
        Assert.Contains(maiVariant.ToString(), json);
        Assert.DoesNotContain(lanVariant.ToString(), json);
    }

    /// <summary>specs/112: the cart and the checkout outcomes go, and nobody else's.</summary>
    [Fact]
    public async Task A_deleted_account_leaves_no_cart_behind()
    {
        var (mai, _) = await PersonWithACartAsync();
        var (lan, lanVariant) = await PersonWithACartAsync();
        await using (var seed = _fixture.For(mai))
        await using (var scope = seed.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
            db.CheckoutOutcomes.Add(new CheckoutOutcome { OrderId = Guid.CreateVersion7(), UserId = mai, UpdatedAt = DateTime.UtcNow });
            db.CheckoutOutcomes.Add(new CheckoutOutcome { OrderId = Guid.CreateVersion7(), UserId = lan, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var provider = _fixture.For(mai);
        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EraseAccountCommand(mai, "mai@example.test"));
            await sender.Send(new EraseAccountCommand(mai, "mai@example.test"));   // a redelivery changes nothing
            var export = await sender.Send(new GetMyDataQuery());
            foreach (var section in CartPersonalData.Inventory.Erased)
                Assert.Empty(export.Sections[section]);

            var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
            Assert.False(await db.CheckoutOutcomes.AnyAsync(o => o.UserId == mai));
            Assert.True(await db.CheckoutOutcomes.AnyAsync(o => o.UserId == lan));
            Assert.True(await db.CartLines.AnyAsync(l => l.VariantId == lanVariant));
        }
    }

    private async Task<(Guid User, Guid Variant)> PersonWithACartAsync()
    {
        var user = Guid.CreateVersion7();
        var cartId = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await using var provider = _fixture.For(user);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        db.Carts.Add(new Domain.Entities.Cart
        {
            Id = cartId, UserId = user, CreatedAt = now, UpdatedAt = now,
            Lines = [new CartLine { Id = Guid.CreateVersion7(), CartId = cartId, ProductId = variant, VariantId = variant, Quantity = 2, AddedAt = now }],
        });
        await db.SaveChangesAsync();
        return (user, variant);
    }
}
