using System.Text.Json;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// The vouchers a shopper could use (specs/114, #220) against the real PostgreSQL: only public, live, priced ones, for
/// the shops asked about and the product named - ending soonest first, and never a limit or a count.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class PublicVoucherTests
{
    private readonly OrderTestFixture _fixture;

    public PublicVoucherTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
    }

    [Fact]
    public async Task A_public_shop_voucher_is_listed_for_its_shop_and_a_private_one_never_is()
    {
        var mai = Guid.CreateVersion7();
        var lan = Guid.CreateVersion7();
        var shown = await VoucherAsync(mai, "Seller", isPublic: true);
        var hidden = await VoucherAsync(mai, "Seller", isPublic: false);
        var lans = await VoucherAsync(lan, "Seller", isPublic: true);
        var platform = await VoucherAsync(Guid.CreateVersion7(), "Admin", isPublic: true);

        var forMai = await ListAsync(new GetPublicVouchersQuery(false, [mai], null, null));

        Assert.Equal([shown], forMai.Select(v => v.Code));
        var one = forMai.Single();
        Assert.Equal((false, mai, "VND", 1_000m), (one.IsPlatform, one.SellerId!.Value, one.Currency, one.FixedValue!.Value));
        Assert.DoesNotContain(hidden, (await ListAsync(new GetPublicVouchersQuery(true, [mai, lan], null, null))).Select(v => v.Code));
        Assert.Contains(lans, (await ListAsync(new GetPublicVouchersQuery(false, [mai, lan], null, null))).Select(v => v.Code));
        Assert.Contains(platform, (await ListAsync(new GetPublicVouchersQuery(true, [mai], null, null))).Select(v => v.Code));
        Assert.DoesNotContain(platform, forMai.Select(v => v.Code));
    }

    [Fact]
    public async Task Nothing_ended_not_started_disabled_used_up_or_unpriced_in_the_currency_is_listed()
    {
        var shop = Guid.CreateVersion7();
        var live = await VoucherAsync(shop, "Seller", isPublic: true, endsAt: DateTime.UtcNow.AddDays(3));
        var ended = await VoucherAsync(shop, "Seller", isPublic: true, startsAt: DateTime.UtcNow.AddDays(-5), endsAt: DateTime.UtcNow.AddDays(-1));
        var later = await VoucherAsync(shop, "Seller", isPublic: true, startsAt: DateTime.UtcNow.AddDays(2));
        var disabled = await VoucherAsync(shop, "Seller", isPublic: true);
        var usedUp = await VoucherAsync(shop, "Seller", isPublic: true, totalLimit: 1);
        var dollars = await VoucherAsync(shop, "Seller", isPublic: true, currency: "USD");
        await DbAsync(db => db.Vouchers.Where(v => v.Code == disabled)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.Status, Domain.Enums.VoucherStatus.Disabled)));
        await DbAsync(db => db.Vouchers.Where(v => v.Code == usedUp).ExecuteUpdateAsync(x => x.SetProperty(v => v.UsedCount, 1)));

        var listed = (await ListAsync(new GetPublicVouchersQuery(false, [shop], null, null))).Select(v => v.Code).ToList();

        Assert.Equal([live], listed);
        _fixture.Currency = new Currency("USD", 2);
        Assert.Equal([dollars], (await ListAsync(new GetPublicVouchersQuery(false, [shop], null, null))).Select(v => v.Code));
        Assert.DoesNotContain(ended, listed);
        Assert.DoesNotContain(later, listed);
    }

    [Fact]
    public async Task For_a_product_only_vouchers_for_everything_or_naming_it_or_one_of_its_variants()
    {
        var shop = Guid.CreateVersion7();
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        var everything = await VoucherAsync(shop, "Seller", isPublic: true);
        var thisOne = await VoucherAsync(shop, "Seller", isPublic: true, targets: [new("Product", product)]);
        var itsVariant = await VoucherAsync(shop, "Seller", isPublic: true, targets: [new("Variant", variant)]);
        var another = await VoucherAsync(shop, "Seller", isPublic: true, targets: [new("Product", Guid.CreateVersion7())]);

        var listed = await ListAsync(new GetPublicVouchersQuery(false, [shop], product, [variant]));

        Assert.Equal(new[] { everything, thisOne, itsVariant }.Order(), listed.Select(v => v.Code).Order());
        Assert.True(listed.Single(v => v.Code == thisOne).Targeted);
        Assert.False(listed.Single(v => v.Code == everything).Targeted);
        Assert.DoesNotContain(itsVariant, (await ListAsync(new GetPublicVouchersQuery(false, [shop], product, null))).Select(v => v.Code));
        Assert.Equal(4, (await ListAsync(new GetPublicVouchersQuery(false, [shop], null, null))).Count);
        Assert.DoesNotContain(another, listed.Select(v => v.Code));
    }

    [Fact]
    public async Task Ending_soonest_comes_first_and_open_ended_last_and_no_limit_or_count_leaves()
    {
        var shop = Guid.CreateVersion7();
        var open = await VoucherAsync(shop, "Seller", isPublic: true, totalLimit: 50);
        var month = await VoucherAsync(shop, "Seller", isPublic: true, endsAt: DateTime.UtcNow.AddDays(30));
        var tomorrow = await VoucherAsync(shop, "Seller", isPublic: true, endsAt: DateTime.UtcNow.AddDays(1));

        var listed = await ListAsync(new GetPublicVouchersQuery(false, [shop], null, null));

        Assert.Equal([tomorrow, month, open], listed.Select(v => v.Code));
        var json = JsonSerializer.Serialize(listed);
        Assert.DoesNotContain("TotalLimit", json);
        Assert.DoesNotContain("UsedCount", json);
        Assert.DoesNotContain("PerCustomerLimit", json);
    }

    [Fact]
    public async Task Asking_about_nobody_is_400()
    {
        await Assert.ThrowsAsync<ValidationException>(() => ListAsync(new GetPublicVouchersQuery(false, null, null, null)));
        await Assert.ThrowsAsync<ValidationException>(() => ListAsync(new GetPublicVouchersQuery(true, null, null, [Guid.CreateVersion7()])));
    }

    [Fact]
    public async Task Created_shown_or_not_and_the_edit_changes_it_or_keeps_it()
    {
        var mai = Guid.CreateVersion7();
        var code = await VoucherAsync(mai, "Seller", isPublic: true);
        var id = await DbAsync(db => db.Vouchers.Where(v => v.Code == code).Select(v => v.Id).SingleAsync());

        var kept = await As(mai, "Seller", () => SendAsync(new EditVoucherCommand("Renamed", null, null, null, null, null) { Id = id }));
        Assert.True(kept.IsPublic);   // a client from before specs/114 sends nothing, and changes nothing

        var hidden = await As(mai, "Seller", () => SendAsync(new EditVoucherCommand("Renamed", null, null, null, null, null, IsPublic: false) { Id = id }));
        Assert.False(hidden.IsPublic);
        Assert.Empty(await ListAsync(new GetPublicVouchersQuery(false, [mai], null, null)));
    }

    [Fact]
    public async Task The_quote_says_which_shop_each_line_is_from()
    {
        var seller = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        _fixture.CurrentUser.Roles.Clear();
        var mine = Guid.CreateVersion7();
        var shops = Guid.CreateVersion7();
        _fixture.Checkout.Prices[mine] = new CatalogPrice(Guid.CreateVersion7(), "Lens", 100_000m, Sellable: true, mine, "SKU-MINE", "", "VND", seller, "Mai's cameras");
        _fixture.Checkout.Prices[shops] = new CatalogPrice(Guid.CreateVersion7(), "Strap", 50_000m, Sellable: true, shops, "SKU-SHOP", "", "VND", null, null);
        _fixture.Checkout.Cart = [new CartItem(Guid.CreateVersion7(), 1, mine), new CartItem(Guid.CreateVersion7(), 1, shops)];
        _fixture.Checkout.Address = new AddressCopy("A", "1 Street", null, "Ha Noi", null, "100000", "VN", null);

        var quote = await SendAsync(new GetCheckoutQuoteQuery(null, "standard", null));

        Assert.Equal(seller, quote.Items.Single(i => i.ProductName == "Lens").SellerId);
        Assert.Null(quote.Items.Single(i => i.ProductName == "Strap").SellerId);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> VoucherAsync(
        Guid owner, string role, bool isPublic, DateTime? startsAt = null, DateTime? endsAt = null, int? totalLimit = null,
        string currency = "VND", List<VoucherTargetRequest>? targets = null)
    {
        var code = $"P{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await As(owner, role, () => SendAsync(new CreateVoucherCommand(code, "Shown voucher", "FixedAmount", null,
            startsAt ?? DateTime.UtcNow.AddMinutes(-1), endsAt, totalLimit, null,
            [new VoucherAmountRequest(currency, currency == "VND" ? 1_000m : 1m, null, null)], null, targets, isPublic)));
        return code;
    }

    private async Task<List<PublicVoucherResponse>> ListAsync(GetPublicVouchersQuery query)
    {
        _fixture.CurrentUser.Id = null;
        _fixture.CurrentUser.Roles.Clear();
        return await SendAsync(query);
    }

    private async Task<T> DbAsync<T>(Func<OrderDbContext, Task<T>> work)
    {
        await using var scope = _fixture.NewScope();
        return await work(scope.ServiceProvider.GetRequiredService<OrderDbContext>());
    }

    private async Task<T> As<T>(Guid user, string role, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = user;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add(role);
        try
        {
            return await body();
        }
        finally
        {
            _fixture.CurrentUser.Id = null;
            _fixture.CurrentUser.Roles.Clear();
        }
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
