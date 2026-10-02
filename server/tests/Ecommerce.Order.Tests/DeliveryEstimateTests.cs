using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Delivery;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Orders.Queries.GetShippingOptions;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Order.Infrastructure.Shipping;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A delivery option says how long it takes (#253, specs/134): an administrator sets it with the option, checkout's list
/// and quote carry it, configuration seeds it for a new option only, and a time that is not one is refused - by the
/// validator in words, and by the table if anything gets past it.
/// </summary>
/// <remarks>Each test works on options of its own and removes them, so the seeded ones other tests price with stay put.</remarks>
[Collection(nameof(OrderTestCollection))]
public class DeliveryEstimateTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;
    private readonly Guid _product = Guid.CreateVersion7();

    [Fact]
    public async Task The_time_set_with_an_option_is_what_checkout_lists_and_quotes_and_clearing_it_says_nothing()
    {
        var code = NewCode();
        try
        {
            await SaveAsync(code, 2, 4);

            var stored = (await SendAsync(new GetDeliverySettingsQuery())).Options.Single(o => o.Code == code);
            Assert.Equal((2, 4), (stored.MinDays, stored.MaxDays));
            var listed = (await ListedAsync()).Single(o => o.Code == code);
            Assert.Equal((2, 4), (listed.MinDays, listed.MaxDays));
            var quoted = await QuoteAsync(code);
            Assert.Equal((2, 4), (quoted.ShippingOption.MinDays, quoted.ShippingOption.MaxDays));

            await SaveAsync(code, null, null);

            var cleared = (await ListedAsync()).Single(o => o.Code == code);
            Assert.Equal(((int?)null, (int?)null), (cleared.MinDays, cleared.MaxDays));
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    [Theory]
    [InlineData(2, null, "MaxDays")]       // one end only
    [InlineData(null, 2, "MinDays")]
    [InlineData(-1, 2, "MaxDays")]         // before today
    [InlineData(3, 61, "MaxDays")]         // beyond the longest
    [InlineData(5, 2, "MaxDays")]          // the soonest after the latest
    public async Task A_time_that_is_not_one_is_refused_naming_the_field(int? min, int? max, string field)
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => SaveAsync(NewCode(), min, max));

        Assert.Contains(refused.Errors, e => e.PropertyName == field);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(0, 60)]
    public async Task The_edges_are_times(int min, int max)
    {
        var code = NewCode();
        try
        {
            var saved = await SaveAsync(code, min, max);
            Assert.Equal((min, max), (saved.MinDays, saved.MaxDays));
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    /// <summary>The CHECK says it again: a write that skips the validator cannot store a time that is not one.</summary>
    [Fact]
    public async Task The_table_refuses_a_time_that_is_not_one()
    {
        var code = NewCode();
        try
        {
            await SaveAsync(code, 1, 2);
            await using var scope = _fixture.NewScope();
            var options = scope.ServiceProvider.GetRequiredService<OrderDbContext>().DeliveryOptions.Where(o => o.Code == code);

            await Assert.ThrowsAnyAsync<Exception>(() => options.ExecuteUpdateAsync(x => x.SetProperty(o => o.MinDays, 5)));
            await Assert.ThrowsAnyAsync<Exception>(() => options.ExecuteUpdateAsync(x => x.SetProperty(o => o.MaxDays, (int?)null)));
            await Assert.ThrowsAnyAsync<Exception>(() => options.ExecuteUpdateAsync(x => x.SetProperty(o => o.MaxDays, 61)));
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    /// <summary>specs/098 still holds: configuration brings a new option's time with it and never changes a stored one.</summary>
    [Fact]
    public async Task Configuration_seeds_a_new_options_time_and_leaves_a_stored_one()
    {
        var code = NewCode();
        var standardBefore = (await SendAsync(new GetDeliverySettingsQuery())).Options.Single(o => o.Code == "standard");
        try
        {
            var configuration = Configuration(new()
            {
                ["Shipping:Options:0:Code"] = "standard",
                ["Shipping:Options:0:Name"] = "Standard delivery",
                ["Shipping:Options:0:Prices:VND"] = "30000",
                ["Shipping:Options:0:MinDays"] = "9",
                ["Shipping:Options:0:MaxDays"] = "9",
                ["Shipping:Options:1:Code"] = code,
                ["Shipping:Options:1:Name"] = "From configuration",
                ["Shipping:Options:1:Prices:VND"] = "20000",
                ["Shipping:Options:1:MinDays"] = "3",
                ["Shipping:Options:1:MaxDays"] = "5",
            });

            await using (var scope = _fixture.NewScope())
            {
                await DeliverySeed.RunAsync(scope.ServiceProvider.GetRequiredService<OrderDbContext>(),
                    new ConfiguredShippingOptions(configuration), configuration);
            }

            var options = (await SendAsync(new GetDeliverySettingsQuery())).Options;
            Assert.Equal((3, 5), (options.Single(o => o.Code == code).MinDays, options.Single(o => o.Code == code).MaxDays));
            var standard = options.Single(o => o.Code == "standard");
            Assert.Equal((standardBefore.MinDays, standardBefore.MaxDays), (standard.MinDays, standard.MaxDays));
        }
        finally
        {
            await RemoveAsync(code);
        }
    }

    [Theory]
    [InlineData("3", null)]
    [InlineData("5", "2")]
    [InlineData("1", "61")]
    public void A_configured_time_that_is_not_one_stops_the_service(string? min, string? max)
    {
        var configuration = Configuration(new()
        {
            ["Shipping:Options:0:Code"] = "standard",
            ["Shipping:Options:0:Name"] = "Standard delivery",
            ["Shipping:Options:0:Prices:VND"] = "30000",
            ["Shipping:Options:0:MinDays"] = min,
            ["Shipping:Options:0:MaxDays"] = max,
        });

        var refused = Assert.Throws<InvalidOperationException>(() => new ConfiguredShippingOptions(configuration));
        Assert.Contains("'standard'", refused.Message);
    }

    // ------------------------------------------------------------------ helpers

    private static string NewCode() => $"t-{Guid.NewGuid():N}"[..20];

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        values["Money:DefaultCurrency"] = "VND";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private Task<DeliveryOptionResponse> SaveAsync(string code, int? min, int? max) =>
        SendAsync(new SaveDeliveryOptionCommand(code, $"Test {code}", true, 900, new() { ["VND"] = 30_000m, ["USD"] = 5m }, min, max));

    private async Task RemoveAsync(string code)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<OrderDbContext>().DeliveryOptions.Where(o => o.Code == code).ExecuteDeleteAsync();
    }

    private Task<IReadOnlyList<Application.Orders.Common.ShippingOptionResponse>> ListedAsync()
    {
        _fixture.Currency = new Ecommerce.Shared.Money.Currency("USD", 2);
        return SendAsync(new GetShippingOptionsQuery());
    }

    private Task<CheckoutQuoteResponse> QuoteAsync(string code)
    {
        _fixture.Currency = new Ecommerce.Shared.Money.Currency("USD", 2);
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.Checkout.Cart = [new CartItem(_product, 1)];
        _fixture.Checkout.Prices[_product] = new CatalogPrice(_product, "Widget", 10m, Sellable: true);
        _fixture.Checkout.Address = new AddressCopy("A", "1 St", null, "City", null, "12345", "US", null);
        return SendAsync(new GetCheckoutQuoteQuery(null, code));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
