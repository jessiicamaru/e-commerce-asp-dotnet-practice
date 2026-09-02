namespace Ecommerce.Contracts.Identity;

public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FullName,
    DateTime RegisteredAt
);
