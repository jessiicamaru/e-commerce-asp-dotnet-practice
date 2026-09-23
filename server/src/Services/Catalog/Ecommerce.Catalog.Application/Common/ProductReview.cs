using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;

namespace Ecommerce.Catalog.Application.Common;

/// <summary>
/// Who sees a product that is not on the shelf, and what sends one back to the moderators (specs/045).
/// </summary>
public static class ProductReview
{
    /// <summary>A seller's listing waits; the shop's own - an administrator listing it - does not.</summary>
    public static bool StartsPending(ICurrentUser currentUser) =>
        currentUser.IsInRole(RoleNames.Seller) && !currentUser.IsInRole(RoleNames.Admin);

    /// <summary>On the shelf, everybody; otherwise its seller and staff only.</summary>
    public static bool MaySee(Product product, ICurrentUser currentUser) =>
        product.IsListed
        || currentUser.IsInRole(StaffRoles.Admin)
        || currentUser.IsInRole(StaffRoles.Moderator)
        || (product.SellerId is not null && product.SellerId == currentUser.Id);

    /// <summary>
    /// A seller changed what a shopper reads or sees - the name, the description, a photograph - on a
    /// product already approved: it goes back to the queue and off the shelf until a moderator looks again
    /// (decided with the user, 2026-09-24). Prices and stock do not come here. Staff edits do not either.
    /// Call before the handler's one save, so the change and the resubmission commit together.
    /// </summary>
    public static async Task AfterSellerEditAsync(
        Product product, ICurrentUser currentUser, IAuditTrail audit, CancellationToken cancellationToken)
    {
        if (product.ReviewStatus != ProductReviewStatus.Approved
            || product.SellerId is null
            || currentUser.IsInRole(StaffRoles.Admin))
        {
            return;
        }

        product.ReviewStatus = ProductReviewStatus.Pending;
        product.ReviewReason = null;
        product.SubmittedAt = DateTime.UtcNow;

        await audit.RecordAsync(
            AuditCategory.Moderation, "ProductSentForReview", "Product", product.Id.ToString(),
            $"\"{product.Name}\" changed after approval and went back to review",
            new { ReviewStatus = "Approved" }, new { ReviewStatus = "Pending" },
            cancellationToken: cancellationToken);
    }
}
