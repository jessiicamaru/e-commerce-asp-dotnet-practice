namespace Ecommerce.Contracts.Identity;

/// <summary>
/// A person deleted their account (specs/112, #217): each service erases or anonymises what it holds about
/// <paramref name="UserId"/>, as its personal-data inventory declares. Published by Identity through its outbox in the
/// transaction that empties the account, beside <see cref="AccessTokensRevoked"/>.
/// </summary>
/// <param name="Email">The address that was theirs - the last place it travels. Activity needs it to take it out of
/// the audit summaries it was written into; nobody stores it.</param>
public record AccountDeleted(Guid UserId, string Email, DateTime DeletedAt);
