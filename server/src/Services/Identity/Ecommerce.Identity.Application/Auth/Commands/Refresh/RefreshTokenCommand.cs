using MediatR;
using Ecommerce.Application.Auth.Common;

namespace Ecommerce.Application.Auth.Commands.Refresh;

/// <param name="Language">What the person reads the shop in now (specs/083) - a language switch reaches their emails at the next renewal.</param>
public record RefreshTokenCommand(string RefreshToken, string Language = "") : IRequest<AuthResponse>;
