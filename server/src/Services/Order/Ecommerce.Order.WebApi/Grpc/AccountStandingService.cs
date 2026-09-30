using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.MyData;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Order.WebApi.Grpc;

/// <summary>
/// Tells Identity what keeps the caller's account from being deleted (specs/112) - Order's first gRPC service.
/// </summary>
/// <remarks>
/// <b>The caller is the forwarded token, not a field.</b> Identity sends the person's bearer token in the call's
/// metadata and this reads them through ICurrentUser, as CartReading does: a request naming the person would let
/// anything on the network ask about anybody.
/// </remarks>
[Authorize]
public class AccountStandingService(ISender mediator) : AccountStanding.AccountStandingBase
{
    private readonly ISender _mediator = mediator;

    public override async Task<AccountStandingReply> GetMyStanding(GetMyStandingRequest request, ServerCallContext context)
    {
        var reply = new AccountStandingReply();
        reply.Blockers.AddRange(await _mediator.Send(new GetMyStandingQuery(), context.CancellationToken));
        return reply;
    }
}
