using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;

namespace Ecommerce.Catalog.Application.Products.Saved;

/// <summary>
/// Telling whoever saved a product that it can be bought again (specs/075) - by whichever route it came back
/// (#182, specs/091): Inventory's announcement, a variant reactivated, any edit whose rollup flips, or a moderator's
/// approval returning it to the shelf.
/// </summary>
/// <remarks>
/// Every caller decides whether this change is the flip - none-to-some, never "still on sale" - and calls this inside
/// the transaction that made it, before its save, so the notices commit with the change or not at all (Principle III).
/// </remarks>
public static class SavedProductNotices
{
    /// <summary>A product somebody can buy: on the shelf, not withdrawn, and in stock.</summary>
    public static bool OnSale(Product product) => product.IsListed && product.IsActive && product.Availability;

    /// <summary>
    /// What an edit hands <see cref="IProductRepository.SaveAndRecomputeRollupAsync"/>: once its rollup has flipped
    /// the product back in stock, tell the savers - if it is on the shelf and not withdrawn; the flip is the stock.
    /// </summary>
    public static Func<CancellationToken, Task> WhenBackInStock(
        IProductRepository products, Guid productId, ISavedProductRepository saved, INotifier notifier, IEmailSender email) =>
        async ct =>
        {
            var product = await products.GetByIdAsync(productId, ct);
            if (product is { IsListed: true, IsActive: true })
            {
                await BackOnSaleAsync(product, saved, notifier, email, ct);
            }
        };

    /// <summary>A notice and an email to each saver. Returns how many were told.</summary>
    public static async Task<int> BackOnSaleAsync(
        Product product,
        ISavedProductRepository saved,
        INotifier notifier,
        IEmailSender email,
        CancellationToken cancellationToken)
    {
        var savers = await saved.SaverIdsAsync(product.Id, cancellationToken);
        foreach (var saver in savers)
        {
            await notifier.NotifyAsync(
                saver, NotificationKind.SavedBackInStock, new Dictionary<string, string> { ["product"] = product.Name },
                $"/products/{product.Id}", cancellationToken);
            // And by email (specs/083) - in the saver's own language, which only Identity knows.
            await email.SendAsync(
                saver, EmailTemplate.SavedBackInStock,
                new Dictionary<string, string> { ["productId"] = product.Id.ToString(), ["product"] = product.Name },
                EmailTemplate.ReadersLanguage, cancellationToken);
        }

        return savers.Count;
    }
}
