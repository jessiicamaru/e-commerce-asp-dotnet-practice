using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Inventory.Application.Common;

/// <summary>
/// Whether the caller may set the stock of this variant (specs/031) — the one place that decides it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusal is a 404, never a 403.</b> A 403 on somebody else's variant confirms the id is real
/// and belongs to someone, which turns a stock endpoint into a way to enumerate the catalogue's
/// private structure. This system already answers that way three times and says why: another
/// customer's address, another customer's order, and another seller's product (specs/011, 003, 027).
/// </para>
/// <para>
/// <b>The words must match a variant that does not exist</b>, or the two are distinguishable after
/// all and the 404 was pointless. They must also NOT match the third 404 this endpoint can answer —
/// "your product is real and yours, but its stock row has not arrived from the broker yet"
/// (specs/031 research D6). That one is reachable only AFTER this check passes and says something
/// different, so a seller retrying knows to retry and a seller refused knows not to.
/// </para>
/// <para>
/// <b>An administrator passes without Catalog being asked at all.</b> They pass every check
/// anyway, so asking would spend a network call to reach a conclusion already known — and would
/// make administering stock fail when Catalog is down, which is exactly when somebody is most
/// likely to be fixing something.
/// </para>
/// <para>
/// <b>A product of the shop's own belongs to administrators and nobody else.</b> A seller cannot
/// adopt it by being the only one asking — the same rule, and the same sentence, as Catalog's
/// <c>SellerOwnership</c>.
/// </para>
/// </remarks>
public static class StockOwnership
{
    /// <summary>
    /// Throws <see cref="NotFoundException"/> unless the caller may write stock for
    /// <paramref name="variantId"/>. Returns having established that they may.
    /// </summary>
    public static async Task RequireCanStockAsync(
        Guid variantId,
        ICurrentUser currentUser,
        IProductOwnership ownership,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Admin))
        {
            return;
        }

        // Live, never from a copy: an ownership answer seconds out of date refuses a seller her own
        // product with this very 404, and nothing can tell that apart from a real refusal.
        var owner = await ownership.GetAsync(variantId, cancellationToken);

        if (owner is null || owner.SellerId is null || owner.SellerId != currentUser.Id)
        {
            throw new NotFoundException(NotFound(variantId));
        }
    }

    /// <summary>
    /// What a variant nobody can name says — whether it is missing or merely somebody else's.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT the "is not registered in inventory" wording the handler uses for a stock
    /// row that has not arrived yet: that one means "try again", this one means "no". Same status,
    /// different sentence, because only one of them is worth retrying.
    /// </remarks>
    public static string NotFound(Guid variantId) =>
        $"Product with ID '{variantId}' was not found.";
}

/// <summary>The role names this service checks against. They must match what Identity signs.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Seller = "Seller";
}
