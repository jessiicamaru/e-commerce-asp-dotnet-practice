namespace Ecommerce.Application.Auth.Common;

public record AuthResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    string RefreshToken
);