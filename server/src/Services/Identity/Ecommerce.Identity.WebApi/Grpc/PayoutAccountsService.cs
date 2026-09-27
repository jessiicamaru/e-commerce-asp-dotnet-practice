using System.Globalization;
using Ecommerce.Application.Sellers;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Domain.Constants;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.WebApi.Grpc;

/// <summary>
/// Gives Order a seller's payout account when an administrator records a payout (specs/106) - the bank, the holder and
/// the last four characters, never the whole number.
/// </summary>
/// <remarks>⚠️ Administrators only: the forwarded token's role is the whole permission (contracts/grpc.md).</remarks>
[Authorize(Roles = RoleNames.Admin)]
public class PayoutAccountsService(ISender mediator) : PayoutAccounts.PayoutAccountsBase
{
    private readonly ISender _mediator = mediator;

    public override async Task<PayoutAccountReply> GetPayoutAccount(GetPayoutAccountRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SellerId, out var sellerId))
        {
            return new PayoutAccountReply { Found = false };
        }

        var account = await _mediator.Send(new GetPayoutAccountForPayoutQuery(sellerId), context.CancellationToken);
        return account is null
            ? new PayoutAccountReply { Found = false }
            : new PayoutAccountReply
            {
                Found = true,
                BankName = account.BankName,
                AccountHolder = account.AccountHolder,
                AccountLast4 = account.Last4,
                UpdatedAt = account.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
            };
    }
}
