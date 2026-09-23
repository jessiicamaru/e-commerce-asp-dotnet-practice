namespace Ecommerce.Domain.Constants;

/// <summary>
/// The roles the system ships with. Seeded on startup by the data initializer; the names are what
/// [Authorize(Roles = "...")] matches against, so they must stay in sync with the database.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    /// <summary>Somebody who lists products of their own (specs/027). Always held WITH Customer:
    /// a seller who cannot buy is a strange kind of account, and every customer-only endpoint
    /// would otherwise refuse them.</summary>
    public const string Seller = "Seller";

    /// <summary>Staff who keep the shop in order (specs/043): lock accounts for up to 30 days, and -
    /// in the features that follow - review shops, products and reviews. Granted only by an
    /// administrator, and the only role that can be.</summary>
    public const string Moderator = "Moderator";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Admin] = "Full administrative access to the platform.",
        [Customer] = "Default role granted to every registered shopper.",
        [Seller] = "Lists and manages their own products; cannot touch anybody else's.",
        [Moderator] = "Reviews what others publish and may lock an account for up to 30 days."
    };
}
