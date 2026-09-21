using MediatR;

namespace Ecommerce.Application.Addresses.Commands.SetDefaultAddress;

public record SetDefaultAddressCommand(Guid Id) : IRequest;
