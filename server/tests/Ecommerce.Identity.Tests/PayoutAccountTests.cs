using System.Reflection;
using System.Text.Json;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Sellers;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using Ecommerce.WebApi.Grpc;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Where a seller's payouts go (#213, specs/106): the seller sees their account masked, a change emails them and is
/// audited masked, and the whole number reaches an administrator paying - and nobody else.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class PayoutAccountTests(IdentityTestFixture fixture)
{
    private const string Number = "0071001234321";

    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_seller_sets_an_account_and_only_ever_sees_it_masked()
    {
        var seller = await _fixture.ApprovedSellerAsync($"pay-{Guid.NewGuid():N}@example.test", "Mai Lens");

        var (set, email, audited) = await SetAsync(seller.Id, new SetMyPayoutAccountCommand(" Vietcombank ", "NGUYEN VAN A", "0071 0012-34321"));

        Assert.Equal(("Vietcombank", "NGUYEN VAN A", "•••• 4321"), (set.BankName, set.AccountHolder, set.AccountNumberMasked));
        var read = await SendAsync(seller.Id, [RoleNames.Seller], new GetMyPayoutAccountQuery());
        Assert.Equal("•••• 4321", read.AccountNumberMasked);
        Assert.DoesNotContain(Number, JsonSerializer.Serialize(read));

        // The email and the audit say which account, never the number.
        Assert.Equal((EmailTemplate.PayoutAccountChanged, seller.Id), (email!.Template, email.RecipientId));
        Assert.Equal("4321", email.Data["last4"]);
        Assert.DoesNotContain(Number, JsonSerializer.Serialize(email.Data));
        Assert.Equal("PayoutAccountSet", audited!.Action);
        Assert.DoesNotContain(Number, audited.After);
        Assert.Contains("4321", audited.After);
    }

    [Fact]
    public async Task Somebody_who_does_not_sell_has_none_and_a_bad_number_is_refused()
    {
        var seller = await _fixture.ApprovedSellerAsync($"pay-{Guid.NewGuid():N}@example.test", "Mai Lens");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(seller.Id, [RoleNames.Seller], new GetMyPayoutAccountQuery()));   // none yet
        foreach (var bad in new[] { "12ab", "0071001234321000999988887777666655554", "0071/0012" })
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                SendAsync(seller.Id, [RoleNames.Seller], new SetMyPayoutAccountCommand("Vietcombank", "NGUYEN VAN A", bad)));
        }
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(seller.Id, [RoleNames.Seller], new SetMyPayoutAccountCommand(" ", "NGUYEN VAN A", Number)));

        var customer = await SendAsync(Guid.Empty, [], new RegisterCommand($"pay-{Guid.NewGuid():N}@example.test", "Passw0rd!23", "Lan", "Pham"));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(customer.Id, [RoleNames.Customer], new SetMyPayoutAccountCommand("Vietcombank", "LAN", Number)));
    }

    [Fact]
    public async Task An_administrator_paying_reads_the_whole_number_and_order_only_the_last_four()
    {
        var seller = await _fixture.ApprovedSellerAsync($"pay-{Guid.NewGuid():N}@example.test", "Mai Lens");
        var none = await _fixture.ApprovedSellerAsync($"pay-{Guid.NewGuid():N}@example.test", "Saigon Lens");
        await SetAsync(seller.Id, new SetMyPayoutAccountCommand("Vietcombank", "NGUYEN VAN A", Number));

        var full = Assert.Single(await SendAsync(Guid.CreateVersion7(), [RoleNames.Admin], new GetPayoutAccountsQuery([seller.Id, none.Id])));
        Assert.Equal((seller.Id, Number), (full.SellerId, full.AccountNumber));

        var forPayout = await SendAsync(Guid.CreateVersion7(), [RoleNames.Admin], new GetPayoutAccountForPayoutQuery(seller.Id));
        Assert.Equal(("Vietcombank", "NGUYEN VAN A", "4321"), (forPayout!.BankName, forPayout.AccountHolder, forPayout.Last4));
        Assert.Null(await SendAsync(Guid.CreateVersion7(), [RoleNames.Admin], new GetPayoutAccountForPayoutQuery(none.Id)));
    }

    /// <summary>The gRPC read is Admin only: the forwarded token's role is the whole permission (contracts/grpc.md).</summary>
    [Fact]
    public void The_payout_account_call_answers_administrators_only()
    {
        var authorize = typeof(PayoutAccountsService).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(RoleNames.Admin, authorize?.Roles);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(PayoutAccountResponse, EmailRequested?, AuditEntryRecorded?)> SetAsync(Guid seller, SetMyPayoutAccountCommand command)
    {
        await using var provider = _fixture.For(seller, [RoleNames.Seller]);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        PayoutAccountResponse set;
        await using (var scope = provider.CreateAsyncScope())
        {
            set = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        return (set,
            harness.Published.Select<EmailRequested>().Select(x => x.Context.Message).SingleOrDefault(e => e.Template == EmailTemplate.PayoutAccountChanged),
            harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).SingleOrDefault(e => e.Action == "PayoutAccountSet"));
    }

    private async Task<T> SendAsync<T>(Guid caller, string[] roles, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
