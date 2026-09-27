using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Delivery;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Shipping;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Administrators manage delivery (#196, specs/098): the table is what checkout charges, an order keeps what it froze, the
/// last option on offer stays on, and configuration only ever adds what is missing.
/// </summary>
/// <remarks>Each test works on options of its own and removes them, so the seeded ones other tests price with stay put.</remarks>
[Collection(nameof(OrderTestCollection))]
public class DeliverySettingsTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;
    private readonly Guid _product = Guid.CreateVersion7();

    [Fact]
    public async Task A_repriced_option_is_what_the_next_checkout_charges_and_an_order_placed_before_keeps_its_price()
    {
        var code = NewCode();
        try
        {
            await SaveAsync(code, prices: new() { ["VND"] = 30_000m, ["USD"] = 5m });
            var before = await CheckoutAsync(code);
            var customer = _fixture.CurrentUser.Id;

            await SaveAsync(code, prices: new() { ["VND"] = 45_000m, ["USD"] = 7.5m });
            var after = await CheckoutAsync(code);

            Assert.Equal(7.5m, after.ShippingPrice);
            _fixture.CurrentUser.Id = customer;   // an order is read by its owner
            var kept = await SendAsync(new GetMyOrderByIdQuery(before.OrderId));
            Assert.Equal(5m, kept.ShippingPrice);
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    [Fact]
    public async Task An_option_turned_off_is_neither_offered_nor_accepted()
    {
        var code = NewCode();
        try
        {
            await SaveAsync(code);
            await SaveAsync(code, isActive: false);

            await using var scope = _fixture.NewScope();
            Assert.Null(scope.ServiceProvider.GetRequiredService<IShippingOptions>().Find(code));
            await Assert.ThrowsAsync<ValidationException>(() => CheckoutAsync(code));
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    [Fact]
    public async Task The_last_option_on_offer_cannot_be_turned_off()
    {
        var code = NewCode();
        List<string> turnedOff = [];
        try
        {
            await SaveAsync(code);
            foreach (var other in (await SendAsync(new GetDeliverySettingsQuery())).Options.Where(o => o.IsActive && o.Code != code))
            {
                await SetActiveAsync(other.Code, false);
                turnedOff.Add(other.Code);
            }

            await Assert.ThrowsAsync<ConflictException>(() => SaveAsync(code, isActive: false));
        }
        finally
        {
            foreach (var other in turnedOff)
                await SetActiveAsync(other, true);
            await RemoveAsync(code);
        }
    }

    [Theory]
    [InlineData("Not A Code", "VND", 1000)]
    [InlineData("ok-code", "EUR", 1000)]     // not a currency this shop sells in
    [InlineData("ok-code", "VND", 9.5)]      // dong has no minor unit
    [InlineData("ok-code", "VND", -1)]
    [InlineData("ok-code", "USD", 2)]        // on offer, but nothing in the shop's own currency
    public async Task An_option_that_is_not_one_is_refused(string code, string currency, decimal amount)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new SaveDeliveryOptionCommand(code, "Test", true, 1, new() { [currency] = amount })));
    }

    [Fact]
    public async Task The_carrier_needs_a_name_and_an_http_address_with_the_reference_in_it()
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SaveCarrierCommand("GHN", "https://ghn.example/track")));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SaveCarrierCommand("GHN", "ftp://ghn.example/{reference}")));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SaveCarrierCommand(" ", null)));

        await SendAsync(new SaveCarrierCommand("GHN", "https://ghn.example/track/{reference}"));

        Assert.Equal(("GHN", "https://ghn.example/track/{reference}"), Pair(await SendAsync(new GetCarrierQuery())));
        await SendAsync(new SaveCarrierCommand("Shop delivery", null));
        Assert.Null((await SendAsync(new GetCarrierQuery())).TrackingUrlTemplate);
    }

    /// <summary>US4: a restart adds what configuration has and the table lacks, and never undoes an administrator's edit.</summary>
    [Fact]
    public async Task Configuration_adds_missing_options_and_changes_nothing_stored()
    {
        var code = NewCode();
        var standardBefore = (await SendAsync(new GetDeliverySettingsQuery())).Options.Single(o => o.Code == "standard");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Shipping:Options:0:Code"] = "standard",
                ["Shipping:Options:0:Name"] = "Renamed by configuration",
                ["Shipping:Options:0:Prices:VND"] = "1",
                ["Shipping:Options:1:Code"] = code,
                ["Shipping:Options:1:Name"] = "From configuration",
                ["Shipping:Options:1:Prices:VND"] = "20000",
                ["Money:DefaultCurrency"] = "VND",
                ["Shipping:Carrier:Name"] = "Not the stored one",
            }).Build();

            await using (var scope = _fixture.NewScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                await DeliverySeed.RunAsync(context, new ConfiguredShippingOptions(configuration), configuration);
            }

            var options = (await SendAsync(new GetDeliverySettingsQuery())).Options;
            Assert.Equal(standardBefore, options.Single(o => o.Code == "standard"), new SameOption());
            var added = options.Single(o => o.Code == code);
            Assert.Equal(("From configuration", 20_000m), (added.Name, added.Prices["VND"]));
            Assert.NotEqual("Not the stored one", (await SendAsync(new GetCarrierQuery())).Name);
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static string NewCode() => $"t-{Guid.NewGuid():N}"[..20];

    private static (string, string?) Pair(CarrierResponse carrier) => (carrier.Name, carrier.TrackingUrlTemplate);

    private Task<DeliveryOptionResponse> SaveAsync(string code, bool isActive = true, Dictionary<string, decimal>? prices = null) =>
        SendAsync(new SaveDeliveryOptionCommand(code, $"Test {code}", isActive, 900, prices ?? new() { ["VND"] = 30_000m, ["USD"] = 5m }));

    private async Task SetActiveAsync(string code, bool active)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().DeliveryOptions.Where(o => o.Code == code)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.IsActive, active));
    }

    private async Task RemoveAsync(string code)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().DeliveryOptions.Where(o => o.Code == code).ExecuteDeleteAsync();
    }

    private async Task<OrderResponse> CheckoutAsync(string code)
    {
        _fixture.Currency = new Ecommerce.Shared.Money.Currency("USD", 2);
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_product, 1)];
        _fixture.Checkout.Prices[_product] = new CatalogPrice(_product, "Widget", 10m, Sellable: true);
        _fixture.Checkout.Address = new AddressCopy("A", "1 St", null, "City", null, "12345", "US", null);
        return await SendAsync(new SubmitOrderCommand(null, code));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private sealed class SameOption : IEqualityComparer<DeliveryOptionResponse>
    {
        public bool Equals(DeliveryOptionResponse? x, DeliveryOptionResponse? y) =>
            x is not null && y is not null && x.Code == y.Code && x.Name == y.Name && x.IsActive == y.IsActive
            && x.Prices.OrderBy(p => p.Key).SequenceEqual(y.Prices.OrderBy(p => p.Key));

        public int GetHashCode(DeliveryOptionResponse obj) => obj.Code.GetHashCode();
    }
}
