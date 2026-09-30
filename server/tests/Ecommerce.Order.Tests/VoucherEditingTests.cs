using Ecommerce.Contracts.Activity;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Correcting a voucher's terms (specs/113, #219) against the real PostgreSQL: what decides whether it applies changes,
/// what an order already used does not; a limit never goes below the uses, even racing checkouts; only the owner edits;
/// and the audit log says what it was and what it became.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class VoucherEditingTests
{
    private static readonly AddressCopy Home =
        new("Nguyen Van A", "12 Ly Thuong Kiet", null, "Ha Noi", null, "100000", "VN", "+84 912 345 678");

    private readonly OrderTestFixture _fixture;

    public VoucherEditingTests(OrderTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Currency = new Currency("VND", 0);
        _fixture.Commission.Current = 0.10m;
    }

    [Fact]
    public async Task Extending_the_end_keeps_the_code_working_and_an_earlier_order_keeps_what_it_used()
    {
        var admin = Guid.CreateVersion7();
        var (id, code) = await VoucherAsync(admin, "Admin", endsAt: DateTime.UtcNow.AddDays(1), fixedValue: 50_000m);
        Customer();
        Cart(100_000m);
        var earlier = await SendAsync(new SubmitOrderCommand(null, "standard", [code]));

        var ends = DateTime.UtcNow.AddDays(30);
        var edited = await As(admin, "Admin", () => SendAsync(Edit(id, "Renamed", ends, totalLimit: 10)));

        Assert.Equal("Renamed", edited.Name);
        Assert.Equal(ends, edited.EndsAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(10, edited.TotalLimit);
        Assert.Equal(1, edited.UsedCount);
        Customer();
        Cart(100_000m);
        Assert.Equal(50_000m, (await SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code]))).DiscountTotal);

        await using var scope = _fixture.NewScope();
        var redemption = await scope.ServiceProvider.GetRequiredService<OrderDbContext>().VoucherRedemptions.AsNoTracking()
            .SingleAsync(r => r.OrderId == earlier.OrderId);
        Assert.Equal(("Test voucher", 50_000m), (redemption.Name, redemption.Amount));   // frozen: the edit reached no order
    }

    [Fact]
    public async Task A_total_limit_below_the_uses_made_is_409_and_changes_nothing()
    {
        var admin = Guid.CreateVersion7();
        var (id, code) = await VoucherAsync(admin, "Admin", totalLimit: 5, fixedValue: 1_000m);
        for (var i = 0; i < 3; i++)
        {
            Customer();
            Cart(100_000m);
            await SendAsync(new SubmitOrderCommand(null, "standard", [code]));
        }

        await Assert.ThrowsAsync<ConflictException>(() => As(admin, "Admin", () => SendAsync(Edit(id, "Lower", null, totalLimit: 2))));

        var now = await ReadAsync(id);
        Assert.Equal(("Test voucher", 5), (now.Name, now.TotalLimit));
        // The statement guards it on its own too (research D2) - what a checkout racing the edit meets.
        await using (var scope = _fixture.NewScope())
        {
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().TryEditAsync(
                id, null, new VoucherEdit("Lower", null, 2, null, new Dictionary<string, decimal?>(), null), DateTime.UtcNow, (_, _) => Task.CompletedTask);
            Assert.Equal(EditOutcome.BelowUses, outcome);
        }

        Assert.Equal(5, (await ReadAsync(id)).TotalLimit);
        var fits = await As(admin, "Admin", () => SendAsync(Edit(id, "Lower", null, totalLimit: 3)));
        Assert.Equal(3, fits.TotalLimit);
    }

    [Fact]
    public async Task Lowering_a_minimum_subtotal_lets_a_checkout_qualify()
    {
        var admin = Guid.CreateVersion7();
        var (id, code) = await VoucherAsync(admin, "Admin", fixedValue: 10_000m, minSubtotal: 500_000m);
        Customer();
        Cart(200_000m);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code])));

        await As(admin, "Admin", () => SendAsync(Edit(id, "Test voucher", null, minSubtotals: [new("vnd", 100_000m)])));

        Customer();
        Cart(200_000m);
        Assert.Equal(10_000m, (await SendAsync(new GetCheckoutQuoteQuery(null, "standard", [code]))).DiscountTotal);
    }

    [Fact]
    public async Task A_minimum_quantity_is_changed_only_where_there_is_one()
    {
        var admin = Guid.CreateVersion7();
        var (withMinimum, _) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m, conditions: [new("MinQuantity", 3)]);
        var (without, _) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m);

        var edited = await As(admin, "Admin", () => SendAsync(Edit(withMinimum, "Test voucher", null, minQuantity: 2)));
        Assert.Equal(2, Assert.Single(edited.Conditions).Value);
        await Assert.ThrowsAsync<ValidationException>(() => As(admin, "Admin", () => SendAsync(Edit(without, "Test voucher", null, minQuantity: 2))));
    }

    [Fact]
    public async Task A_disabled_voucher_is_409_and_a_currency_it_does_not_have_is_400()
    {
        var admin = Guid.CreateVersion7();
        var (id, _) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m);
        await Assert.ThrowsAsync<ValidationException>(() => As(admin, "Admin", () => SendAsync(Edit(id, "X", null, minSubtotals: [new("USD", 10m)]))));

        await As(admin, "Admin", () => SendAsync(new DisableVoucherCommand(id)));

        await Assert.ThrowsAsync<ConflictException>(() => As(admin, "Admin", () => SendAsync(Edit(id, "X", null))));
    }

    /// <summary>Research D4: an end in the past is what disabling is for; an end before the start is no voucher at all.</summary>
    [Fact]
    public async Task An_end_in_the_past_or_before_the_start_is_400()
    {
        var admin = Guid.CreateVersion7();
        var (id, _) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m, startsAt: DateTime.UtcNow.AddDays(-10));
        var (later, _) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m, startsAt: DateTime.UtcNow.AddDays(10));

        // After the start, but already over.
        await Assert.ThrowsAsync<ValidationException>(() => As(admin, "Admin", () => SendAsync(Edit(id, "X", DateTime.UtcNow.AddDays(-1)))));
        await Assert.ThrowsAsync<ValidationException>(() => As(admin, "Admin", () => SendAsync(Edit(later, "X", DateTime.UtcNow.AddDays(5)))));
        Assert.Null((await ReadAsync(id)).EndsAt);
    }

    [Fact]
    public async Task Only_the_owner_edits_and_an_administrator_edits_the_platforms()
    {
        var mai = Guid.CreateVersion7();
        var (maiVoucher, _) = await VoucherAsync(mai, "Seller", fixedValue: 1_000m);
        var (platform, _) = await VoucherAsync(Guid.CreateVersion7(), "Admin", fixedValue: 1_000m);

        await Assert.ThrowsAsync<NotFoundException>(() => As(Guid.CreateVersion7(), "Seller", () => SendAsync(Edit(maiVoucher, "Mine now", null))));
        await Assert.ThrowsAsync<NotFoundException>(() => As(Guid.CreateVersion7(), "Admin", () => SendAsync(Edit(maiVoucher, "Staff's words", null))));
        await Assert.ThrowsAsync<NotFoundException>(() => As(mai, "Seller", () => SendAsync(Edit(platform, "Mine now", null))));
        await Assert.ThrowsAsync<NotFoundException>(() => As(mai, "Seller", () => SendAsync(Edit(Guid.CreateVersion7(), "Nothing", null))));

        Assert.Equal("Mai's words", (await As(mai, "Seller", () => SendAsync(Edit(maiVoucher, "Mai's words", null)))).Name);
        Assert.Equal("Test voucher", (await ReadAsync(platform)).Name);
    }

    [Fact]
    public async Task The_audit_log_holds_what_it_was_and_what_it_became()
    {
        var admin = Guid.CreateVersion7();
        var (id, code) = await VoucherAsync(admin, "Admin", fixedValue: 1_000m, totalLimit: 50);

        await As(admin, "Admin", () => SendAsync(Edit(id, "Audited", null, totalLimit: 80)));

        var entry = _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Last(e => e.Action == "VoucherEdited" && e.SubjectId == id.ToString());
        Assert.Equal($"Voucher {code} edited", entry.Summary);
        Assert.Contains("\"totalLimit\":50", entry.Before);
        Assert.Contains("\"totalLimit\":80", entry.After);
        Assert.Contains("\"name\":\"Audited\"", entry.After);
    }

    /// <summary>Research D2: an edit lowering the limit races checkouts claiming uses - five rounds, so at least one
    /// really overlaps. Whatever wins, the uses never exceed the limit that stands.</summary>
    [Fact]
    public async Task An_edit_racing_checkouts_never_leaves_more_uses_than_the_limit()
    {
        for (var round = 0; round < 5; round++)
        {
            var (id, code) = await VoucherAsync(Guid.CreateVersion7(), "Admin", fixedValue: 1_000m, totalLimit: 6);
            Customer();
            Cart(100_000m);

            var checkouts = Enumerable.Range(0, 6).Select(async _ =>
            {
                try { await SendAsync(new SubmitOrderCommand(null, "standard", [code])); }
                catch (ConflictException) { }
            });
            var edit = Task.Run(async () =>
            {
                await using var scope = _fixture.NewScope();
                await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().TryEditAsync(
                    id, null, new VoucherEdit("Raced", null, 2, null, new Dictionary<string, decimal?>(), null), DateTime.UtcNow,
                    (_, _) => Task.CompletedTask);
            });
            await Task.WhenAll(checkouts.Append(edit));

            var now = await ReadAsync(id);
            Assert.True(now.UsedCount <= now.TotalLimit, $"round {round}: {now.UsedCount} uses against a limit of {now.TotalLimit}");
        }
    }

    // ------------------------------------------------------------------ helpers

    private static EditVoucherCommand Edit(
        Guid id, string name, DateTime? endsAt, int? totalLimit = null, int? perCustomerLimit = null,
        List<VoucherMinSubtotalRequest>? minSubtotals = null, int? minQuantity = null) =>
        new(name, endsAt, totalLimit, perCustomerLimit, minSubtotals, minQuantity) { Id = id };

    private Guid Customer()
    {
        var customer = Guid.CreateVersion7();
        _fixture.CurrentUser.Id = customer;
        _fixture.CurrentUser.Roles.Clear();
        return customer;
    }

    private void Cart(decimal price)
    {
        var product = Guid.CreateVersion7();
        var variant = Guid.CreateVersion7();
        _fixture.Checkout.Prices[variant] = new CatalogPrice(
            product, "Camera", price, Sellable: true, variant, $"SKU-{variant:N}"[..12], "", "VND", null, null);
        _fixture.Checkout.Cart = [new CartItem(product, 1, variant)];
        _fixture.Checkout.Address = Home;
    }

    private async Task<(Guid Id, string Code)> VoucherAsync(
        Guid creator, string role, DateTime? endsAt = null, int? totalLimit = null, decimal? fixedValue = null, decimal? minSubtotal = null,
        List<VoucherConditionRequest>? conditions = null, DateTime? startsAt = null)
    {
        var code = $"E{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var created = await As(creator, role, () => SendAsync(new CreateVoucherCommand(code, "Test voucher", "FixedAmount", null,
            startsAt ?? DateTime.UtcNow.AddMinutes(-1), endsAt, totalLimit, null, [new VoucherAmountRequest("VND", fixedValue, null, minSubtotal)], conditions, null)));
        return (created.Id, code);
    }

    private async Task<Domain.Entities.Voucher> ReadAsync(Guid id)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Vouchers.AsNoTracking().SingleAsync(v => v.Id == id);
    }

    private async Task<T> As<T>(Guid user, string role, Func<Task<T>> body)
    {
        var who = (_fixture.CurrentUser.Id, _fixture.CurrentUser.Roles.ToList());
        _fixture.CurrentUser.Id = user;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add(role);
        try
        {
            return await body();
        }
        finally
        {
            _fixture.CurrentUser.Id = who.Item1;
            _fixture.CurrentUser.Roles.Clear();
            foreach (var r in who.Item2) _fixture.CurrentUser.Roles.Add(r);
        }
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
