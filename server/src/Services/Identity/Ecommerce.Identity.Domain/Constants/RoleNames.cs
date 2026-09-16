namespace Ecommerce.Domain.Constants;

/// <summary>
/// The roles the system ships with. Seeded on startup by the data initializer; the names are what
/// [Authorize(Roles = "...")] matches against, so they must stay in sync with the database.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Admin] = "Full administrative access to the platform.",
        [Customer] = "Default role granted to every registered shopper."
    };
}
