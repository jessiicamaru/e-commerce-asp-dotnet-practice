using MediatR;
using Ecommerce.Application.Auth.Common;

namespace Ecommerce.Application.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string Language = ""   // what the person reads the shop in - their emails' language (specs/083); from Accept-Language
) : IRequest<AuthResponse>;