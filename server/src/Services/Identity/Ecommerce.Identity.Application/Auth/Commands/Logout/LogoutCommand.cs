using MediatR;

namespace Ecommerce.Application.Auth.Commands.Logout;

/// <summary>Ends the session that <paramref name="RefreshToken"/> belongs to (feature 015).</summary>
public record LogoutCommand(string? RefreshToken) : IRequest;
