using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Infrastructure.Identity;

/// <summary>
/// Reads one of the caller's addresses from Identity, forwarding the caller's own token to do it.
/// </summary>
/// <remarks>
/// The third synchronous dependency of checkout, after Catalog and Cart (specs/011 research D1). Same
/// shape as <see cref="Cart.GrpcCartReader"/>: the request names an address, never a user - Identity
/// validates the forwarded token itself and answers only for that customer's address book.
/// </remarks>
public class GrpcAddressReader(
    AddressReading.AddressReadingClient client,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GrpcAddressReader> logger) : IAddressReader
{
    private readonly AddressReading.AddressReadingClient _client = client;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GrpcAddressReader> _logger = logger;

    private const int MaxAttempts = 3;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)];

    public async Task<AddressCopy?> GetMyAddressAsync(Guid? addressId, CancellationToken cancellationToken = default)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization))
        {
            throw new UnauthorizedAccessException("No token to forward to the Identity service.");
        }

        var headers = new Metadata { { "Authorization", authorization } };
        var request = new GetMyAddressRequest { AddressId = addressId?.ToString() ?? string.Empty };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _client.GetMyAddressAsync(
                    request,
                    headers,
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                if (!response.Found)
                {
                    return null;
                }

                var a = response.Address;

                return new AddressCopy(
                    a.RecipientName,
                    a.Line1,
                    string.IsNullOrEmpty(a.Line2) ? null : a.Line2,
                    a.City,
                    string.IsNullOrEmpty(a.Region) ? null : a.Region,
                    a.PostalCode,
                    a.Country,
                    string.IsNullOrEmpty(a.Phone) ? null : a.Phone);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                throw new UnauthorizedAccessException("The Identity service did not accept the forwarded token.");
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                if (attempt >= MaxAttempts)
                {
                    _logger.LogWarning(ex, "Identity did not answer after {Attempts} attempts.", MaxAttempts);

                    throw new DependencyUnavailableException(
                        $"The delivery address could not be read after {MaxAttempts} attempts, so the order "
                        + "was not placed. Nothing was charged and no stock was reserved.");
                }

                await Task.Delay(Backoff[attempt - 1], cancellationToken);
            }
        }
    }
}
