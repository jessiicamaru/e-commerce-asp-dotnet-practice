using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A voucher list searched by code or name and filtered by state (specs/133, #249) - in SQL, so the total is the
/// filtered list's. Each test is a seller of its own, whose vouchers are the only ones "mine" returns.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class VoucherFilterTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Search_matches_the_code_or_the_name_in_any_case()
    {
        var seller = Guid.CreateVersion7();
        var summer = await VoucherAsync(seller, "SUMMER", "Summer sale");
        var lens = await VoucherAsync(seller, "LENS", "Lenses 10% off");
        await VoucherAsync(seller, "STRAP", "Straps");

        Assert.Equal([summer], Codes(await MineAsync(seller, search: "summ")));
        Assert.Equal([lens], Codes(await MineAsync(seller, search: "lenses")));
        Assert.Equal(3, (await MineAsync(seller)).TotalCount);
    }

    [Fact]
    public async Task A_state_lists_only_its_own_with_its_own_total()
    {
        var seller = Guid.CreateVersion7();
        var active = await VoucherAsync(seller, "ACTIVE", "Running");
        var ended = await VoucherAsync(seller, "ENDED", "Was running");
        var disabled = await VoucherAsync(seller, "OFF", "Turned off");
        await DbAsync(async db =>
        {
            var row = await db.Vouchers.SingleAsync(v => v.Code == ended);
            row.EndsAt = DateTime.UtcNow.AddDays(-1);
            return await db.SaveChangesAsync();
        });
        var id = await DbAsync(db => db.Vouchers.Where(v => v.Code == disabled).Select(v => v.Id).SingleAsync());
        await As(seller, () => SendAsync(new DisableVoucherCommand(id)));

        var activeOnes = await MineAsync(seller, state: VoucherListState.Active);
        Assert.Equal([active], Codes(activeOnes));
        Assert.Equal(1, activeOnes.TotalCount);
        Assert.Equal([ended], Codes(await MineAsync(seller, state: VoucherListState.Ended)));
        Assert.Equal([disabled], Codes(await MineAsync(seller, state: VoucherListState.Disabled)));
    }

    [Fact]
    public async Task An_unknown_state_is_refused()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => MineAsync(Guid.CreateVersion7(), state: "Expired"));
        Assert.Contains(refused.Errors, e => e.PropertyName == "State");
    }

    private static List<string> Codes(Application.Orders.Common.PagedResponse<VoucherSummary> page) =>
        page.Items.Select(v => v.Code).ToList();

    private async Task<string> VoucherAsync(Guid seller, string prefix, string name)
    {
        var code = $"{prefix}{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        await As(seller, () => SendAsync(new CreateVoucherCommand(code, name, "FixedAmount", null, DateTime.UtcNow.AddMinutes(-1),
            null, null, null, [new VoucherAmountRequest("VND", 1_000m, null, null)], null, null)));
        return code;
    }

    private Task<Application.Orders.Common.PagedResponse<VoucherSummary>> MineAsync(Guid seller, string? search = null, string? state = null) =>
        As(seller, () => SendAsync(new GetMyVouchersQuery(1, 50, search, state)));

    private async Task<T> DbAsync<T>(Func<OrderDbContext, Task<T>> work)
    {
        await using var scope = _fixture.NewScope();
        return await work(scope.ServiceProvider.GetRequiredService<OrderDbContext>());
    }

    private async Task<T> As<T>(Guid user, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = user;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add("Seller");
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
