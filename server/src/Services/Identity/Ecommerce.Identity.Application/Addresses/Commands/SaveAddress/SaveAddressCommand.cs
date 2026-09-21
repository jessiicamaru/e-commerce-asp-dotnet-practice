using Ecommerce.Application.Addresses.Common;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.SaveAddress;

/// <remarks>No user field: the owner is the caller, read from the token (Constitution IV).</remarks>
public record SaveAddressCommand(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    string? Phone
) : IRequest<AddressResponse>, IAddressFields;
