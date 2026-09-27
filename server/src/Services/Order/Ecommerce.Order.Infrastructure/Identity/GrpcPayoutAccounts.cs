using System.Globalization;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Infrastructure.Identity;

/// <summary>
/// Reads a seller's payout account from Identity when an administrator records a payout (specs/106), forwarding the
/// administrator's token - Identity serves it to the Admin role only. The same shape as <see cref="GrpcAddressReader"/>.
/// </summary>
/// <remarks>
/// A synchronous edge, justified in research D2: the destination must be frozen when the payout is recorded, and the
/// alternatives put bank details on the broker. Called before the claim's transaction, never inside it.
/// </remarks>
public class GrpcPayoutAccounts(
    PayoutAccounts.PayoutAccountsClient client,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GrpcPayoutAccounts> logger) : IPayoutAccounts
{
    private readonly PayoutAccounts.PayoutAccountsClient _client = client;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GrpcPayoutAccounts> _logger = logger;

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public async Task<PayoutDestination?> GetAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            throw new UnauthorizedAccessException("No token to forward to the Identity service.");

        try
        {
            var reply = await _client.GetPayoutAccountAsync(
                new GetPayoutAccountRequest { SellerId = sellerId.ToString() },
                new Metadata { { "Authorization", authorization } },
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: cancellationToken);

            if (!reply.Found)
                return null;

            return new PayoutDestination(reply.BankName, reply.AccountHolder, reply.AccountLast4,
                DateTime.Parse(reply.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
        {
            throw new UnauthorizedAccessException("The Identity service did not accept the forwarded token for a payout account.");
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Identity did not answer for seller {SellerId}'s payout account.", sellerId);
            // Refused, never recorded without a destination (spec edge case).
            throw new DependencyUnavailableException("The seller's payout account could not be read, so no payout was recorded. Try again.");
        }
    }
}
