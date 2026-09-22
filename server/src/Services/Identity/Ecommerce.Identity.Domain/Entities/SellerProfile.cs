namespace Ecommerce.Domain.Entities;

/// <summary>
/// The shop behind a seller's account (specs/027).
/// </summary>
/// <remarks>
/// <para>
/// <b>The user id is the primary key</b>, not a surrogate. There is exactly one shop per account, and
/// a separate <c>Id</c> would let a second row exist - making "one shop per account" a convention
/// somebody has to remember rather than something the database refuses.
/// </para>
/// <para>
/// The name lives here and is copied into Catalog as a read model, so a product listing can name the
/// seller without a call per card. Renaming the shop changes what the catalogue shows and writes to
/// no product.
/// </para>
/// </remarks>
public class SellerProfile
{
    /// <summary>The account this shop belongs to. Primary key and foreign key both.</summary>
    public Guid UserId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
