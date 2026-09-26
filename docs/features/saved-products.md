# Saved products

A signed-in shopper can **save a product for later** with the heart on a product card or on the product page. The
saved products are listed at `/saved`, and the shopper is told when one of them comes back in stock (specs/075,
#109). The cart is where things go to be bought: it holds a quantity. A saved product is something the shopper has
not decided about yet.

The list lives in **Catalog**, next to the products it names. Reading it costs no call to another service, and the
list reads each product the way the listing does, in the shopper's language and currency.

## What people can do

| Role | Capabilities |
| :-- | :-- |
| Anyone signed in | Save a product that is on sale, unsave it, and read their own list, newest first. |
| Signed out | Sees the hearts. Tapping one sends them to sign in and back to the page they were on. |

## Rules and guarantees

1. **The list is the caller's own.** Who is asking comes from the token, never from the request (Constitution IV).
   Nobody can read or change another shopper's list, and no endpoint takes a shopper id.
2. **Saving is once per product**, however many times or how many requests race.
   - Saving is `INSERT ... ON CONFLICT DO NOTHING` on the key `(CustomerId, ProductId)`.
   - A second save keeps the first `SavedAt`.
   - Unsaving something that was never saved is not an error.
3. **Only a product on sale can be saved.** This is the public lookup's rule (specs/045): approved and active, or
   it is the same 404 as an id that does not exist. A 403 or a different error would confirm that a hidden
   product exists.
4. **What the shopper saved stays until they unsave it.**
   - A product that was withdrawn, rejected or taken down after it was saved stays in the list with
     `available: false`. A list that silently loses things is one nobody trusts.
   - A product **deleted** outright leaves the list, because the row cascades from `products`.
   - `available` means on sale, active and in stock: the things that decide whether it can be bought now.
5. **The product is read as it is now.** The price is today's, in the currency the request asks for, and the name
   is in the language it asks for. Nothing about the product is frozen at the moment it was saved: this is not
   an order.
6. **Back in stock is told once per flip.**
   - The notice goes out when the product's rollup turns from out of stock to in stock, which happens when
     Inventory reports that a variant has units again.
   - The flip is read inside the one `UPDATE` that recomputes the rollup: a CTE reads the value the statement
     started from. A concurrent write therefore cannot make two statements both believe they flipped it.
   - Another "still in stock" announcement tells nobody.
   - Each saver gets `SavedBackInStock` with `{product}` and a link to the product page. Like every notice, it is
     published through the outbox before the handler's one save (specs/042).
   - A product that is **not on sale** when it comes back in stock tells nobody. The shopper could not buy it.

## Data

`saved_products`, in `ecommerce_catalog_db`:

| Column | Type | Notes |
| :-- | :-- | :-- |
| `CustomerId` | `uuid` | The shopper, from the token. Part of the key. |
| `ProductId` | `uuid` | Foreign key to `products`, `ON DELETE CASCADE`. Part of the key. |
| `SavedAt` | `timestamptz` | The first save. |

The indexes:
- `(CustomerId, SavedAt)` reads a shopper's page newest first.
- `ProductId` finds the savers when a product comes back.

Migration `20260926095302_AddSavedProducts` only adds, so an earlier image still runs against it.

## API

| Method | Path | Who | Answers |
| :-- | :-- | :-- | :-- |
| `PUT` | `/api/products/{id}/saved` | Signed in | 204. A product that is not on sale is 404. |
| `DELETE` | `/api/products/{id}/saved` | Signed in | 204, whether or not it was saved. |
| `GET` | `/api/products/saved?page=&pageSize=` | Signed in | A page of `{ product, savedAt, available }`, newest first. `product` is the listing's `ProductResponse`. |
| `GET` | `/api/products/saved/ids` | Signed in | The ids alone, which is enough to draw every heart on a page of cards with one request. |

The existing `/api/products/*` gateway route carries all four. No new message is added: the notice is
`UserNotificationRequested`, which is kept by Activity (specs/042).

## Storefront

| Path | What it does |
| :-- | :-- |
| `client/src/components/product/save-button/` | The heart, `aria-pressed` when saved. It sits beside the card's link rather than inside it, because a button inside a link is invalid HTML. |
| `client/src/pages/saved/` | `/saved`: the cards, newest first. One that cannot be bought is dimmed and says so. It shows `PAGE_SIZE` per page. |
| `client/src/hooks/saved-product/` | `useSavedIds`, which makes one request shared by every heart and is off while signed out. It also holds `useToggleSaved` and `useSavedProducts`. |
| `client/src/services/saved-product/` | `SavedProduct.save`, `unsave`, `list` and `ids`. |
| `client/src/components/layout/user-menu/` | **Saved** in the account menu. |

The words are in `catalog` (`saved.*`), `common` (`nav.saved`) and `notifications` (`kind.SavedBackInStock`).

## Tests

| Where | What it proves |
| :-- | :-- |
| `Ecommerce.Catalog.Tests/SavedProductTests` (7) | Saving twice, or twenty times at once, keeps one entry. Unsaving is idempotent. A product not on sale cannot be saved, and a made-up id cannot either. The list is the caller's own, newest first, in the listing's words. A product taken down stays in the list as unavailable, and a deleted one goes. Back in stock tells each saver once per flip: two shoppers and two flips give four notices. A product off the shelf tells nobody. |
| `client/src/components/product/save-button/index.test.tsx` | The heart reads the saved ids. Tapping it saves or unsaves. Signed out, it goes to sign in. |
| `client/src/pages/saved/index.test.tsx` | The list, and a product that is no longer available. |
| `client/src/services/saved-product/index.test.ts` | The URLs, with no shopper id. |
| `bruno/product/` 61-66 | Save (204), save again (204), the ids hold it once, the list's shape, unsave (204), and 401 without a token. |

Mutation checks (specs/075): each of these turns `SavedProductTests` red.

| Mutation | Test that fails |
| :-- | :-- |
| Removing `ON CONFLICT` | The twenty-at-once test |
| Letting an unlisted product be saved | The on-sale test |
| Ignoring "was" in the flip | The once-per-flip test |
| Notifying for a product off the shelf | The off-the-shelf test |

## Known limits

- **No "price dropped" notice**, and no sharing a list.
- **A shopper with many saved products gets one notice per product that flips.** There is no digest.
- **The back-in-stock email names the product in its default language**, as the notice does: Catalog does not
  know the saver's language, and Identity writes the rest of the email in it (specs/083).
