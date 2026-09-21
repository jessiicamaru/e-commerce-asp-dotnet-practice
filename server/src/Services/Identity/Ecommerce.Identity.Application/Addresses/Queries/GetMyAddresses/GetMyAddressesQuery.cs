using Ecommerce.Application.Addresses.Common;
using MediatR;

namespace Ecommerce.Application.Addresses.Queries.GetMyAddresses;

public record GetMyAddressesQuery : IRequest<List<AddressResponse>>;
