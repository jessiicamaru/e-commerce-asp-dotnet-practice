using MediatR;
using Ecommerce.Application.Auth.Common;

namespace Ecommerce.Application.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponse>;