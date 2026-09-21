using Ecommerce.Application.Addresses.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Application.Addresses.Queries.GetMyAddress;

public class GetMyAddressQueryHandler(
    IAddressRepository addresses,
    ICurrentUser currentUser) : IRequestHandler<GetMyAddressQuery, AddressResponse?>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<AddressResponse?> Handle(GetMyAddressQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var address = request.Id is { } id
            ? await _addresses.GetAsync(userId, id, cancellationToken)
            : await _addresses.GetDefaultAsync(userId, cancellationToken);

        return address is null ? null : AddressResponse.From(address);
    }
}
