# Contracts: The picture follows the variant

> Completed on 2026-09-27, after the feature merged (#73), from the code at that merge, the pull request and
> docs/features/catalog.md. HTTP only: no message and no proto changed.

## Three new routes, nested like the price and translation ones

| Method | Path | Who |
| :-- | :-- | :-- |
| `PUT` | `/api/products/{id}/variants/{variantId}/image` | Seller (own), Admin |
| `DELETE` | `/api/products/{id}/variants/{variantId}/image` | Seller (own), Admin |
| `GET` | `/api/products/{id}/variants/{variantId}/image` | anyone |

Same rules as the product image and enforced the same way: one multipart part named `file`, type
from the **bytes**, at most 2 MB, SVG refused. Somebody else's is **404**, never 403.

### As built (from the code at #73)

| Route | Answers |
| :-- | :-- |
| `PUT …/image` | `204`; `400` (`errors.File`) for no `file` part, an empty file, over 2 MB, or bytes that are not JPEG, PNG or WebP; `404 Variant with ID '{variantId}' was not found.` for a variant that does not exist, belongs to a different product, or belongs to another seller; `409` when a concurrent change won; `401`/`403` at the door |
| `DELETE …/image` | `204`, also when the variant has no photograph of its own (quiet, it keeps falling back); `404` and `409` as above |
| `GET …/image?v=` | `200` with the bytes, `Content-Type` from the row, `X-Content-Type-Options: nosniff`, and `Cache-Control: public, max-age=31536000, immutable` when `v` matches the current version (`no-cache` otherwise); `404` when the variant has no photograph of its own - callers are handed the product's address instead |

The request size limit on `PUT` is `ProductImageKey.MaxBytes + 64 KiB`, so a 2 MB file plus multipart
framing reaches the handler and a larger one is refused before it is read in full.

Both writes are `[Authorize(Roles = "Seller,Admin")]`; the handler decides ownership through
`SellerOwnership.CanWrite` - not `RequireCanWrite`, whose message names the **product** id and would
make "not yours" distinguishable from "no such variant".

> Later features changed the address and are not part of this contract: since specs/081 an image is
> served only with its own access key (`&k=`), and since specs/079 the bytes live in S3. Use the
> `imageUrl` a response gives rather than composing one.

## The response gains one nullable field

```diff
 VariantResponse(
     Guid Id, string Sku, decimal? Price, string Currency,
     string OptionSummary, List<VariantOptionResponse> Options,
     string Availability, bool IsActive,
+    string? ImageUrl
 )
```

⚠️ **Already resolved when it arrives.** `ImageUrl` is the variant's own address when it has an
image and **the product's** when it does not (research D2), so the storefront renders what it is
given rather than implementing the fallback in two places and getting one of them wrong. It is null
only when neither has one.

`ProductResponse.ImageUrl` is unchanged — the listing card keeps showing the product's own picture
(research D7).

## The storage key needs a discriminator, and this is not defensive

⚠️ **The first variant of a product REUSES the product's id** (specs/020). Measured on the live
catalogue: **12 of 12** products have a variant whose id equals the product id.

`ProductImageKey.For(id, updatedAt, format)` renders `{id:N}-{ticks}.{ext}`, so for that variant the
product's key and the variant's key differ **only by their timestamps** — two separate
`ImageUpdatedAt` columns that happen not to agree. Set both in the same tick and they are the same
filename, and one image silently overwrites the other.

```text
product  01a0…c3   ImageUpdatedAt 638…100   ->  01a0…c3-638…100.jpg
variant  01a0…c3   ImageUpdatedAt 638…100   ->  01a0…c3-638…100.jpg     # the same file
```

So variant keys are prefixed:

```text
product image   {id:N}-{ticks}.{ext}                unchanged
variant image   variant-{id:N}-{ticks}.{ext}        new
```

**The product form is left exactly as it is** so the images already on the `catalog_images` volume
keep resolving — this ships without moving a single existing file.

## Messages and gRPC

None changed. `ProductDeletedEvent` is published exactly as before; `CatalogPricing` and
`CatalogOwnership` carry no image.

## Deleting a product

No contract change. `DELETE /api/products/{id}` already returns 204 and publishes
`ProductDeletedEvent` unchanged; it now also deletes **every variant's** image alongside the
product's, in the order and with the swallow specs/029 settled — row first, bytes after, a store
failure logged and never fatal.

## No new `Vary`

Checked, not assumed. `Vary: Accept-Language` and `Vary: X-Currency` exist because one address
returns different representations per header. A variant image address names one variant and returns
one file whatever the language or currency, so it varies on nothing. `Cache-Control: immutable` plus
the version in the query string is the whole caching story, as for a product image.
