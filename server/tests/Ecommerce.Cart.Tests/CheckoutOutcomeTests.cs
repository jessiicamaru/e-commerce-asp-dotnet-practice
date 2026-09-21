using Ecommerce.Cart.Application.Carts.Commands.AddToCart;
using Ecommerce.Cart.Application.Checkout;
using Ecommerce.Cart.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Cart.Tests;

/// <summary>
/// What an order removes from a cart - once, only on completion, whichever order the events come in.
/// </summary>
[Collection(nameof(CartTestCollection))]
public class CheckoutOutcomeTests(CartTestFixture fixture)
{
    private readonly CartTestFixture _fixture = fixture;

    private static readonly Guid X = Guid.CreateVersion7();
    private static readonly Guid Y = Guid.CreateVersion7();

    [Fact]
    public async Task Completion_after_submission_removes_what_was_ordered()
    {
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));
        await Outcomes(user, o => o.RecordCompletedAsync(order));

        Assert.Empty(await Lines(user));
    }

    [Fact]
    public async Task Completion_BEFORE_submission_still_removes_them()
    {
        // The case that matters. Nothing orders delivery across message types - issue #15 was this
        // exact shape. Handled only the likely way round, the completion would do nothing and the
        // late submission would record items that nothing ever applied: the cart would never clear.
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordCompletedAsync(order));

        Assert.Equal(2, (await Lines(user))[X]);   // nothing to apply yet - no items known

        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));

        Assert.Empty(await Lines(user));
    }

    [Fact]
    public async Task A_repeated_completion_removes_once_and_spares_what_was_added_since()
    {
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));
        await Outcomes(user, o => o.RecordCompletedAsync(order));

        // The customer buys X again after the order, then the broker redelivers the completion.
        await Add(user, X, 1);
        await Outcomes(user, o => o.RecordCompletedAsync(order));
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));

        Assert.Equal(1, (await Lines(user))[X]);
    }

    [Fact]
    public async Task It_removes_what_was_ordered_not_everything()
    {
        // During checkout the customer raises X to 5 and adds Y. The order was for X x2.
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));

        await Add(user, X, 3);   // now 5
        await Add(user, Y, 1);

        await Outcomes(user, o => o.RecordCompletedAsync(order));

        var lines = await Lines(user);
        Assert.Equal(3, lines[X]);
        Assert.Equal(1, lines[Y]);
    }

    [Fact]
    public async Task A_failed_order_leaves_the_cart_alone()
    {
        // Failed is terminal in this system, so removing lines here would leave a customer whose
        // card was declined with an empty cart and a dead order.
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));
        await Outcomes(user, o => o.RecordFailedAsync(order));

        Assert.Equal(2, (await Lines(user))[X]);
    }

    [Fact]
    public async Task A_stray_failure_cannot_undo_a_completion()
    {
        var user = Guid.NewGuid();
        await Add(user, X, 2);

        var order = Guid.NewGuid();
        await Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 2)]));
        await Outcomes(user, o => o.RecordCompletedAsync(order));
        await Add(user, X, 4);
        await Outcomes(user, o => o.RecordFailedAsync(order));
        await Outcomes(user, o => o.RecordCompletedAsync(order));

        Assert.Equal(4, (await Lines(user))[X]);
    }

    [Fact]
    public async Task Submission_and_completion_arriving_at_once_apply_exactly_once()
    {
        // Two consumers, same order, same instant. The row lock serialises them, so one of them is
        // "second" and applies; neither both nor neither.
        var user = Guid.NewGuid();
        await Add(user, X, 10);

        for (var round = 0; round < 10; round++)
        {
            var order = Guid.NewGuid();
            await Task.WhenAll(
                Outcomes(user, o => o.RecordSubmittedAsync(order, user, [new(X, 1)])),
                Outcomes(user, o => o.RecordCompletedAsync(order)));
        }

        Assert.Empty(await Lines(user));
    }

    [Fact]
    public async Task Two_first_ever_adds_at_once_make_one_cart()
    {
        var user = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Add(user, X, 1)));

        // Twenty concurrent adds of the same product: one cart, one line, quantity 20.
        Assert.Equal(20, (await Lines(user))[X]);
    }

    // ---------------------------------------------------------------- helpers

    private async Task Add(Guid user, Guid product, int quantity)
    {
        await using var provider = _fixture.For(user);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new AddToCartCommand(product, quantity));
    }

    private async Task Outcomes(Guid user, Func<CheckoutOutcomes, Task> act)
    {
        await using var provider = _fixture.For(user);
        await using var scope = provider.CreateAsyncScope();
        await act(scope.ServiceProvider.GetRequiredService<CheckoutOutcomes>());
    }

    private async Task<Dictionary<Guid, int>> Lines(Guid user)
    {
        await using var provider = _fixture.For(user);
        await using var scope = provider.CreateAsyncScope();
        var cart = await scope.ServiceProvider.GetRequiredService<ICartRepository>().GetAsync(user);
        return cart?.Lines.ToDictionary(l => l.ProductId, l => l.Quantity) ?? [];
    }
}

/// <summary>
/// Validation actually runs on commands that return nothing.
/// </summary>
/// <remarks>
/// It did not. The shared ValidationBehavior was constrained to <c>where TRequest : IRequest&lt;TResponse&gt;</c>,
/// and in MediatR 12 a command that returns nothing implements <c>IRequest</c> - a separate interface,
/// not <c>IRequest&lt;Unit&gt;</c>. The pipeline asked for <c>IPipelineBehavior&lt;AddToCartCommand, Unit&gt;</c>,
/// the constraint could not be met, and the behavior was skipped without a word. A quantity of -1 was
/// accepted with 204. Found by running the quickstart, not by reading the code.
/// </remarks>
[Collection(nameof(CartTestCollection))]
public class ValidationTests(CartTestFixture fixture)
{
    private readonly CartTestFixture _fixture = fixture;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Adding_a_non_positive_quantity_is_rejected(int quantity)
    {
        await using var provider = _fixture.For(Guid.NewGuid());
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            scope.ServiceProvider.GetRequiredService<MediatR.ISender>()
                .Send(new Ecommerce.Cart.Application.Carts.Commands.AddToCart.AddToCartCommand(Guid.NewGuid(), quantity)));
    }
}
