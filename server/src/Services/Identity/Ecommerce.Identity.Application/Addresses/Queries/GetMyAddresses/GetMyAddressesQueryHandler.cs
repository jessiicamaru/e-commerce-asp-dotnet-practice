using Ecommerce.Application.Addresses.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Application.Addresses.Queries.GetMyAddresses;

public class GetMyAddressesQueryHandler(
    IAddressRepository addresses,
    ICurrentUser currentUser) : IRequestHandler<GetMyAddressesQuery, List<AddressResponse>>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<List<AddressResponse>> Handle(GetMyAddressesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var all = await _addresses.ListAsync(userId, cancellationToken);

        return all
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.CreatedAt)
            .Select(AddressResponse.From)
            .ToList();
    }
}
