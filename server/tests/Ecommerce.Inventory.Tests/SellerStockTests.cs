using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// A seller sets the stock of what is theirs, and only that (specs/031, issue #70).
/// </summary>
/// <remarks>
/// <para>
/// Before this, a seller could register, open a shop, list a product and price it — and never sell
/// it, because <c>PUT /api/stock/{id}</c> on her own product answered 403 and the listing read
/// <c>OutOfStock</c> until an administrator typed a number.
/// </para>
/// <para>
/// The refusals here are <b>404, never 403</b>, and worded exactly like a variant that does not
/// exist: a 403 would confirm the id is real and belongs to somebody.
/// </para>
/// </remarks>
[Collection(nameof(InventoryTestCollection))]
public class SellerStockTests(InventoryTestFixture fixture) : IDisposable
{
    private readonly InventoryTestFixture _fixture = fixture;

    /// <summary>
    /// ⚠️ The caller is a shared singleton. Leaving it as a seller broke eleven Catalog image tests
    /// when SellerOwnershipTests did exactly this, so it is put back here rather than remembered.
    /// </summary>
    public void Dispose()
    {
        _fixture.Caller.BeAdmin();
        _fixture.Owners.Clear();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<Guid> ARegisteredVariantAsync()
    {
        var variantId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(variantId, $"SKU-{variantId:N}"[..20]));
        return variantId;
    }

    [Fact]
    public async Task A_seller_sets_the_stock_of_their_own_product()
    {
        var alice = Guid.CreateVersion7();
        var variantId = await ARegisteredVariantAsync();
        _fixture.Owners.OwnedBy(variantId, alice);
        _fixture.Caller.BeSeller(alice);

        var stock = await SendAsync(new SetStockOnHandCommand(variantId, 3));

        Assert.Equal(3, stock.QuantityOnHand);
        Assert.Equal(3, stock.QuantityAvailable);
    }

    [Fact]
    public async Task Another_sellers_product_is_NOT_FOUND_and_never_forbidden()
    {
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var variantId = await ARegisteredVariantAsync();
        _fixture.Owners.OwnedBy(variantId, alice);
        _fixture.Caller.BeSeller(bob);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new SetStockOnHandCommand(variantId, 99)));

        // Word for word what a variant nobody can name says.
        Assert.Contains("was not found", error.Message);

        // ...and hers is untouched.
        _fixture.Caller.BeAdmin();
        var stock = await SendAsync(new SetStockOnHandCommand(variantId, 3));
        Assert.Equal(3, stock.QuantityOnHand);
    }

    /// <summary>
    /// A product of the shop's own (no seller) belongs to administrators and nobody else - a seller
    /// cannot adopt it by being the only one asking. Same rule, same sentence, as Catalog's
    /// SellerOwnership.
    /// </summary>
    [Fact]
    public async Task A_seller_cannot_stock_the_shops_own_product()
    {
        var variantId = await ARegisteredVariantAsync();
        _fixture.Owners.OwnedBy(variantId, sellerId: null);
        _fixture.Caller.BeSeller(Guid.CreateVersion7());

        await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new SetStockOnHandCommand(variantId, 5)));
    }

    [Fact]
    public async Task An_administrator_still_stocks_anything()
    {
        var variantId = await ARegisteredVariantAsync();
        _fixture.Owners.OwnedBy(variantId, Guid.CreateVersion7());   // somebody else's

        var stock = await SendAsync(new SetStockOnHandCommand(variantId, 7));

        Assert.Equal(7, stock.QuantityOnHand);
    }

    /// <summary>
    /// An administrator passes without Catalog being asked at all: they pass every check anyway, and
    /// asking would make administering stock fail exactly when somebody is fixing something.
    /// </summary>
    [Fact]
    public async Task An_administrator_does_not_cost_a_call_to_Catalog()
    {
        var variantId = await ARegisteredVariantAsync();
        _fixture.Owners.Clear();
        _fixture.Owners.Throws = new InvalidOperationException("Catalog must not be asked.");

        var stock = await SendAsync(new SetStockOnHandCommand(variantId, 4));

        Assert.Equal(4, stock.QuantityOnHand);
        Assert.Equal(0, _fixture.Owners.Calls);
    }

    /// <summary>
    /// The trap from specs/031 research D6. The stock row is created off the broker, so a seller can
    /// reach here moments after listing and find no row. That 404 must NOT read like the ownership
    /// one: this one means "try again", that one means "no".
    /// </summary>
    [Fact]
    public async Task A_stock_row_that_has_not_arrived_yet_says_something_different()
    {
        var alice = Guid.CreateVersion7();
        var neverRegistered = Guid.CreateVersion7();
        _fixture.Owners.OwnedBy(neverRegistered, alice);      // hers, per Catalog...
        _fixture.Caller.BeSeller(alice);                      // ...but Inventory has no row yet

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => SendAsync(new SetStockOnHandCommand(neverRegistered, 2)));

        Assert.Contains("not registered in inventory", error.Message);
        Assert.DoesNotContain("was not found", error.Message);
    }

    /// <summary>
    /// "I could not find out whether this is yours" is not "this is not yours". Collapsing them
    /// would tell a seller she does not own her own shop whenever Catalog hiccuped.
    /// </summary>
    [Fact]
    public async Task Catalog_being_unreachable_is_not_a_refusal()
    {
        var variantId = await ARegisteredVariantAsync();
        _fixture.Caller.BeSeller(Guid.CreateVersion7());
        _fixture.Owners.Throws = new DependencyUnavailableException("The catalogue could not be reached.");

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => SendAsync(new SetStockOnHandCommand(variantId, 1)));
    }
}
