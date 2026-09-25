using MediatR;
using Ecommerce.Application.Auth.Common;

namespace Ecommerce.Application.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Language = ""   // the confirmation email's language; the controller reads Accept-Language (specs/063)
) : IRequest<AuthResponse>;