using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Catalog.Application.Common;

/// <summary>
/// Whether the caller may write to this product (specs/027) - the one place that decides it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusal is a 404, never a 403.</b> A 403 on somebody else's product confirms the id is real and
/// that it belongs to someone, which turns a listing endpoint into an enumeration tool. The system
/// already answers this way twice and says why: another customer's address and another customer's
/// order are both "not found" (specs/011, specs/003).
/// </para>
/// <para>
/// <b>An administrator passes everything</b>, because moderating a marketplace is the job. An
/// administrator's own products belong to the shop itself, not to them.
/// </para>
/// <para>
/// It lives here rather than in a controller attribute for a reason an attribute cannot solve: an
/// attribute runs before anything is read and therefore cannot know who owns a row. Nine write
/// operations call this one method; a check copied into nine handlers would be correct in eight.
/// </para>
/// </remarks>
public static class SellerOwnership
{
    /// <summary>
    /// Throws <see cref="NotFoundException"/> unless the caller may write to <paramref name="product"/>.
    /// </summary>
    public static void RequireCanWrite(Product product, ICurrentUser currentUser)
    {
        if (CanWrite(product, currentUser))
        {
            return;
        }

        // Word for word what a missing product says, because that is the point.
        throw new NotFoundException($"Product with ID '{product.Id}' was not found.");
    }

    public static bool CanWrite(Product product, ICurrentUser currentUser)
    {
        if (currentUser.IsInRole(RoleNames.Admin))
        {
            return true;
        }

        // A product of the shop's own (SellerId null) is an administrator's to manage and nobody
        // else's - a seller cannot adopt it by being the only one asking.
        return product.SellerId is not null && product.SellerId == currentUser.Id;
    }
}

/// <summary>The role names this service checks against. They must match what Identity signs.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Seller = "Seller";
}
