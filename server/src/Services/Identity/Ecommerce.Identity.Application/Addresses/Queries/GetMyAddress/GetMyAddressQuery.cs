using Ecommerce.Application.Addresses.Common;
using MediatR;

namespace Ecommerce.Application.Addresses.Queries.GetMyAddress;

/// <summary>One of the caller's addresses; <c>null</c> <paramref name="Id"/> means their default.</summary>
/// <remarks>
/// Answers <c>null</c> - never throws - for "not found", "not yours" and "no default", which are
/// deliberately indistinguishable. The REST endpoint turns that into 404; the gRPC endpoint into
/// found = false.
/// </remarks>
public record GetMyAddressQuery(Guid? Id) : IRequest<AddressResponse?>;
