using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Who makes which vouchers, and what a voucher may say (specs/069): an administrator's is the platform's, a
/// seller's is their shop's - from the token, never the body; a seller never sees or disables another's; and
/// every amount is one its currency can hold.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class VoucherManagementTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task An_administrators_voucher_is_the_platforms_and_a_sellers_is_their_shops()
    {
        var admin = Guid.CreateVersion7();
        var mai = Guid.CreateVersion7();

        var platform = await As(admin, "Admin", () => SendAsync(Voucher(Code())));
        var shop = await As(mai, "Seller", () => SendAsync(Voucher(Code())));

        Assert.True(platform.IsPlatform);
        Assert.False(shop.IsPlatform);
        Assert.Equal(mai, await OwnerAsync(shop.Id));
        Assert.Null(await OwnerAsync(platform.Id));
        Assert.Contains(_fixture.Harness.Published.Select<AuditEntryRecorded>(),
            p => p.Context.Message.Action == "VoucherCreated" && p.Context.Message.SubjectId == shop.Id.ToString());
    }

    [Fact]
    public async Task Codes_are_stored_upper_case_and_one_taken_in_any_case_is_a_409()
    {
        var code = Code();
        var created = await As(Guid.CreateVersion7(), "Admin", () => SendAsync(Voucher(code.ToLowerInvariant())));
        Assert.Equal(code, created.Code);

        var taken = await Assert.ThrowsAsync<ConflictException>(() => As(Guid.CreateVersion7(), "Seller", () => SendAsync(Voucher(code))));
        Assert.Contains("taken", taken.Message);
    }

    [Fact]
    public async Task What_a_voucher_may_say_is_checked_before_anything_is_saved()
    {
        var seller = Guid.CreateVersion7();

        await Refused("Seller", Voucher(Code()) with { Benefit = "FreeShipping", Percent = null }, "platform's");
        await Refused("Admin", Voucher(Code()) with { Conditions = [new VoucherConditionRequest("FirstOrderInShop", null)] }, "FirstOrderInShop");
        await Refused("Admin", Voucher(Code()) with { Amounts = [new VoucherAmountRequest("VND", null, 10.5m, null)] }, "dong has no decimals");
        await Refused("Admin", Voucher(Code()) with { Amounts = [new VoucherAmountRequest("EUR", null, null, null)] }, "Currency is one of");
        await Refused("Admin", Voucher(Code()) with { Amounts = [] }, "never converted");
        await Refused("Admin", Voucher(Code()) with { Benefit = "FixedAmount", Percent = null }, "FixedValue above 0");
        await Refused("Admin", Voucher(Code()) with { Percent = 120m }, "1 to 100");
        await Refused("Admin", Voucher("x!"), "3-32 letters");
        await Refused("Admin", Voucher(Code()) with { Targets = [new VoucherTargetRequest("Category", Guid.CreateVersion7())] }, "Product or a Variant");
        await Refused("Admin", Voucher(Code()) with { TotalLimit = 1, PerCustomerLimit = 2 }, "more uses");
        await Refused("Admin", Voucher(Code()) with { StartsAt = DateTime.UtcNow, EndsAt = DateTime.UtcNow.AddDays(-1) }, "end after it starts");

        async Task Refused(string role, CreateVoucherCommand command, string words)
        {
            var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => As(seller, role, () => SendAsync(command)));
            Assert.Contains(words, refused.Message);
        }
    }

    [Fact]
    public async Task Each_sees_their_own_a_seller_theirs_and_an_administrator_the_platforms()
    {
        var mai = Guid.CreateVersion7();
        var bao = Guid.CreateVersion7();
        var hers = await As(mai, "Seller", () => SendAsync(Voucher(Code())));
        await As(bao, "Seller", () => SendAsync(Voucher(Code())));
        var platform = await As(Guid.CreateVersion7(), "Admin", () => SendAsync(Voucher(Code())));

        var mine = await As(mai, "Seller", () => SendAsync(new GetMyVouchersQuery()));
        var admins = await As(Guid.CreateVersion7(), "Admin", () => SendAsync(new GetMyVouchersQuery(1, 50)));

        Assert.Equal([hers.Id], mine.Items.Select(v => v.Id));
        Assert.Contains(admins.Items, v => v.Id == platform.Id);
        Assert.All(admins.Items, v => Assert.True(v.IsPlatform));
    }

    [Fact]
    public async Task A_seller_disables_their_own_not_anothers_and_an_administrator_any()
    {
        var mai = Guid.CreateVersion7();
        var hers = await As(mai, "Seller", () => SendAsync(Voucher(Code())));
        var another = await As(mai, "Seller", () => SendAsync(Voucher(Code())));

        await Assert.ThrowsAsync<NotFoundException>(() => As(Guid.CreateVersion7(), "Seller", () => SendAsync(new DisableVoucherCommand(hers.Id))));
        Assert.Equal("Disabled", (await As(mai, "Seller", () => SendAsync(new DisableVoucherCommand(hers.Id)))).Status);
        await Assert.ThrowsAsync<ConflictException>(() => As(mai, "Seller", () => SendAsync(new DisableVoucherCommand(hers.Id))));
        Assert.Equal("Disabled", (await As(Guid.CreateVersion7(), "Admin", () => SendAsync(new DisableVoucherCommand(another.Id)))).Status);
        await Assert.ThrowsAsync<NotFoundException>(() => As(mai, "Seller", () => SendAsync(new DisableVoucherCommand(Guid.CreateVersion7()))));
        Assert.Single(_fixture.Harness.Published.Select<AuditEntryRecorded>(),
            p => p.Context.Message.Action == "VoucherDisabled" && p.Context.Message.SubjectId == hers.Id.ToString());
    }

    // ------------------------------------------------------------------ helpers

    private static string Code() => $"M{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    private static CreateVoucherCommand Voucher(string code) => new(
        code, "Ten percent", "Percent", 10m, null, null, null, null,
        [new VoucherAmountRequest("VND", null, 100_000m, 500_000m)], null, null);

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
            _fixture.CurrentUser.Roles.Clear();
        }
    }

    private async Task<Guid?> OwnerAsync(Guid voucher)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Vouchers.Where(v => v.Id == voucher).Select(v => v.SellerId).SingleAsync();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
