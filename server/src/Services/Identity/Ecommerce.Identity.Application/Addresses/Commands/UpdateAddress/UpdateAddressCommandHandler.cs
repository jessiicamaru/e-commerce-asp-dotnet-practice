using Ecommerce.Application.Addresses.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.UpdateAddress;

/// <remarks>
/// Editing an address changes only the address book. Orders placed with it hold their own copy and
/// are untouched (FR-008).
/// </remarks>
public class UpdateAddressCommandHandler(
    IAddressRepository addresses,
    ICurrentUser currentUser) : IRequestHandler<UpdateAddressCommand, AddressResponse>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<AddressResponse> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var address = await _addresses.GetAsync(userId, request.Id, cancellationToken)
            ?? throw new NotFoundException("Address not found.");

        address.RecipientName = request.RecipientName.Trim();
        address.Line1 = request.Line1.Trim();
        address.Line2 = string.IsNullOrWhiteSpace(request.Line2) ? null : request.Line2.Trim();
        address.City = request.City.Trim();
        address.Region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim().ToUpperInvariant();
        address.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        address.UpdatedAt = DateTime.UtcNow;

        await _addresses.SaveChangesAsync(cancellationToken);

        return AddressResponse.From(address);
    }
}
