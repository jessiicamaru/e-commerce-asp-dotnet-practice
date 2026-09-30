using Ecommerce.Application.Auth.Commands.DeleteAccount;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.Order;

/// <summary>
/// Asks Order what keeps the caller's account open (specs/112) - Identity's first call to another service.
/// </summary>
/// <remarks>
/// <b>The caller's own token is forwarded</b>, and the request is empty: Order decides whose standing from the token,
/// as Cart does for a cart. Asked live and never cached - a permission read from a copy seconds behind would let an
/// account go with an order still on its way (specs/031). Order unreachable is a 503: an account is never deleted
/// without the check.
/// </remarks>
public class GrpcAccountStanding(
    AccountStanding.AccountStandingClient client,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GrpcAccountStanding> logger) : IAccountStanding
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    private readonly AccountStanding.AccountStandingClient _client = client;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GrpcAccountStanding> _logger = logger;

    public async Task<IReadOnlyList<string>> GetMyBlockersAsync(CancellationToken cancellationToken = default)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            // Deleting is signed in, so this is a wiring fault, not the person's mistake.
            throw new UnauthorizedAccessException("No token to forward to the Order service.");
        }

        try
        {
            var reply = await _client.GetMyStandingAsync(
                new GetMyStandingRequest(),
                new Metadata { { "Authorization", authorization } },
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: cancellationToken);
            return reply.Blockers.ToList();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            throw new UnauthorizedAccessException("The Order service did not accept the forwarded token.");
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Order did not answer whether the account may be deleted.");
            throw new DependencyUnavailableException(
                "Whether this account still has orders open could not be checked, so it was not deleted. Try again in a moment.");
        }
    }
}
