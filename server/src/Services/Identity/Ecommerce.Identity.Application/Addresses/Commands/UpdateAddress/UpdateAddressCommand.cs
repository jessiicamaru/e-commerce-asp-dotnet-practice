using Ecommerce.Application.Addresses.Common;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.UpdateAddress;

/// <remarks>
/// <paramref name="Id"/> names an address, never a user. If it is somebody else's, it is simply not
/// found - the owner is always the caller.
/// </remarks>
public record UpdateAddressCommand(
    Guid Id,
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    string? Phone
) : IRequest<AddressResponse>, IAddressFields;
