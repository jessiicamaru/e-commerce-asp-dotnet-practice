using Ecommerce.Application.Addresses.Queries.GetMyAddress;
using Ecommerce.Contracts.Grpc;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.WebApi.Grpc;

/// <summary>
/// Gives Order one of the caller's addresses at checkout.
/// </summary>
/// <remarks>
/// <b>The caller is identified by the forwarded token, not by anything in the request</b> - Order
/// sends the customer's bearer token in the call's metadata, and the query reads the user through
/// ICurrentUser exactly as the REST endpoints do. "Not found", "not yours" and "no default" all come
/// back as found = false, indistinguishably (specs/011 research D4).
/// </remarks>
[Authorize]
public class AddressReadingService(ISender mediator) : AddressReading.AddressReadingBase
{
    private readonly ISender _mediator = mediator;

    public override async Task<GetMyAddressResponse> GetMyAddress(GetMyAddressRequest request, ServerCallContext context)
    {
        Guid? id = null;

        if (!string.IsNullOrEmpty(request.AddressId))
        {
            if (!Guid.TryParse(request.AddressId, out var parsed))
            {
                return new GetMyAddressResponse { Found = false };
            }

            id = parsed;
        }

        var address = await _mediator.Send(new GetMyAddressQuery(id), context.CancellationToken);

        if (address is null)
        {
            return new GetMyAddressResponse { Found = false };
        }

        return new GetMyAddressResponse
        {
            Found = true,
            Address = new DeliveryAddress
            {
                Id = address.Id.ToString(),
                RecipientName = address.RecipientName,
                Line1 = address.Line1,
                Line2 = address.Line2 ?? string.Empty,
                City = address.City,
                Region = address.Region ?? string.Empty,
                PostalCode = address.PostalCode,
                Country = address.Country,
                Phone = address.Phone ?? string.Empty
            }
        };
    }
}
