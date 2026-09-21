using MediatR;

namespace Ecommerce.Application.Addresses.Commands.DeleteAddress;

public record DeleteAddressCommand(Guid Id) : IRequest;
