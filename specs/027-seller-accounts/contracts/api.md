# Contracts: The Shop Is a Marketplace

## Identity

| Endpoint | Change |
| :-- | :-- |
| `POST /api/auth/register-seller` | everything `register` takes plus `shopName` → an account holding **`Seller` and `Customer`**, and a token saying so |
| `PUT /api/sellers/me/shop-name` — **Seller** | `{ shopName }` → 200. Changes what the catalogue shows on every one of their products, with no write to any product |
| `GET /api/sellers/me` — **Seller** | the caller's own shop |

**There is no endpoint that takes a seller id.** Who the seller is comes from the token, the way the
user id has since specs/009. A body that names a seller is the defect this project has already fixed
twice.

## Catalog

`ProductResponse` gains:

```jsonc
{
  "sellerId": "…" | null,   // null = the shop itself, which is every product from before this
  "sellerName": "…" | null  // from Catalog's own read model; null when it is the shop itself
}
```

| Endpoint | Change |
| :-- | :-- |
| `POST /api/products` | **Seller or Admin.** A seller's product records them; an administrator's records nobody, and is the shop's |
| `GET /api/products/mine` — **Seller** | their listings, and only theirs |
| every product write | a seller writing to a product that is not theirs gets **404** |

The writes that now check ownership: update variant, add variant, delete product, set/remove
translation, set/remove variant price, set option translation, upload/remove image. **Admin passes
every one of them.**

## Messages

```csharp
namespace Ecommerce.Contracts.Identity;

/// A new shop exists. Catalog keeps the name so a listing costs no extra call.
public record SellerRegisteredEvent(Guid SellerId, string ShopName, DateTime RegisteredAt);

/// A shop changed its name. Nothing about any product changes.
public record SellerRenamedEvent(Guid SellerId, string ShopName, DateTime RenamedAt);
```

Both are consumed by Catalog and by nothing else. The timestamp is the guard: an overtaken rename
that arrives late must not win, which is the rule `StockAvailabilityChangedEvent` already follows.

## Storefront

- `Sold by …` shows the shop name, and the shop's own name when there is none. The line already
  exists (specs/025) and was laid out for this.
- Sign-up offers "sell on this shop", which asks for a shop name.
- **No seller console.** Managing listings is the API's surface for now — recorded in research D5.
