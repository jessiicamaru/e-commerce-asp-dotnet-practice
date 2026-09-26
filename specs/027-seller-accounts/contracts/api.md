# Contracts: The Shop Is a Marketplace

> Completed on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

The integration messages are also described, with publisher, consumer and idempotency, in
[messages.md](messages.md) (added 2026-09-27).

## Identity

| Endpoint | Change |
| :-- | :-- |
| `POST /api/auth/register-seller` | everything `register` takes plus `shopName` → an account holding **`Seller` and `Customer`**, and a token saying so |
| `PUT /api/sellers/me/shop-name` — **Seller** | `{ shopName }` → 200. Changes what the catalogue shows on every one of their products, with no write to any product |
| `GET /api/sellers/me` — **Seller** | the caller's own shop |

Responses and refusals (added 2026-09-27 from the code):

| Endpoint | Success | Refusals |
| :-- | :-- | :-- |
| `POST /api/auth/register-seller` (anonymous) | `200` with `AuthResponse` | `400` - email, password, names, `shopName` required and at most 100 characters; `409` "An account with this email already exists." |
| `GET /api/sellers/me` | `200` `{ sellerId, shopName, createdAt }` | `401`; `403` not a seller; `404` "This account does not sell on the shop." |
| `PUT /api/sellers/me/shop-name` | `200` `{ sellerId, shopName, createdAt }` | `400` empty or over 100 characters; `401`; `403`; `404` |

Both `/api/sellers` paths reach Identity through the gateway routes `sellers-route` and
`sellers-root-route`, added in this change - without them the rename was a 404 that looked like a missing
endpoint.

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
| every product write | `[Authorize(Roles = "Seller,Admin")]` (was `Admin`); a seller writing to a product that is not theirs gets **404** |
| `GET /api/products`, `GET /api/products/{id}` | anonymous, unchanged; each item now carries `sellerId` and `sellerName` |

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
  *Corrected on 2026-09-27:* **not built in #64.** The PR changed only `product-card`, the product page and
  the product type; registering as a seller was API-only at this merge.
- **No seller console.** Managing listings is the API's surface for now — recorded in research D5.
