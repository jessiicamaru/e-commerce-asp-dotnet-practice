# Implementation Plan: A shopper saves a product for later

**Branch**: `075-saved-products` | **Spec**: [spec.md](spec.md) | **Issue**: #109

## Design (Catalog)

- The `SavedProduct` entity and its configuration, plus the migration `AddSavedProducts`.
- `ISavedProductRepository`:
  - `SaveAsync` (`ON CONFLICT DO NOTHING`);
  - `UnsaveAsync` (a guarded delete);
  - `GetPageAsync(customerId, page, size)`, which returns products with their translations and variant prices, the
    same includes as the listing, plus `SavedAt`;
  - `IdsAsync(customerId)`;
  - `SaverIdsAsync(productId)`.
- `SavedProductFeatures.cs` holds the commands, the queries and one handler class.
  - Saving checks `Product.IsListed` and `IsActive`. Anything else is a 404, as the public lookup is.
  - The page maps each product with the listing's own response (the language and the currency of the request),
    wrapped in `SavedProductResponse(Product, SavedAt, Available)`. `Available` means listed, active and in stock.
- **Back in stock.** `RecomputeProductRollupAsync` returns whether the product's `Availability` went from false
  to true.
  - That is one statement: a CTE reads the old value under `FOR UPDATE`, then the UPDATE runs.
  - `RecordStockAvailabilityCommandHandler` then notifies each saver of a listed product, through the consumer's
    outbox, in the same transaction.
- `SavedProductsController`, at `api/products` with `[Authorize]`: the routes `{id}/saved`, `saved` and
  `saved/ids`. They do not collide with `{id:guid}`, because `saved` is not a guid.

## Storefront

- The `SavedProduct` service, and the hooks `useSavedIds`, `useToggleSaved` and `useSavedProducts`.
- `components/product/save-button` is the heart, with `aria-pressed`, on the product card and on the product page.
- `pages/saved` is routed at `/saved` and linked from the user menu.
- The notification kind `SavedBackInStock`, in `vi` and `en`.

## Research

- **D1 - Catalog owns it.** A saved product is about products: price, availability and listing status are all
  Catalog's. The cart is another thing (a quantity, meant to be bought).
- **D2 - a hidden product cannot be saved, but one saved before it was hidden stays.** Saving is asking about
  a public product. The list is the shopper's own record, and it says "no longer available" rather than
  silently dropping something they chose.
- **D3 - the notice is on the rollup's flip.** Inventory announces per variant, many times. Only the product
  going from none in stock to some in stock is news.
