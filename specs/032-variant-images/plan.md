# Implementation Plan: The picture follows the variant

**Branch**: `032-variant-images` | **Spec**: [spec.md](spec.md) | **Closes**: #72

## Technical Context

Two nullable columns on `product_variants` mirroring the two on `products`, a prefixed store key,
three nested routes, a resolved `ImageUrl` on the variant response, the chooser swapping the
picture, and an extension to the product-delete cleanup.

**One migration, purely additive.** Two nullable columns; nothing dropped, renamed or narrowed, so
an earlier image still starts against the new schema.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Untouched. No new cross-service call; Catalog owns its own images. |
| II - Clean Architecture | Bytes stay behind `IProductImageStore`; handlers never see a directory. |
| III - Atomic writes, idempotent messaging | The write is the existing guarded `UPDATE` pattern: new file, switch the row, delete the old. No new message. The delete's file removal stays **outside** the transaction, as specs/029 settled. |
| IV - Identity from the token | `SellerOwnership` decides, in the handler. No route gains a seller id. |
| V - Evidence over assumption | The per-option-value design was **rejected on measured data**, and the key collision was measured too: 12 of 12 products have a variant whose id equals the product id. |

No Complexity Tracking entries.

## The three traps

**1. The reused id.** The first variant of a product shares the product's id, on every product in
the catalogue. Without a discriminator in the key, the product's image and that variant's image are
the same filename whenever their timestamps agree. Prefixing variant keys is the fix; leaving the
product form alone is what keeps the existing files resolving.

**2. The controller attribute.** specs/027 shipped with ownership checks unreachable because the
attribute still said `Admin`; specs/031 nearly repeated it. Three new routes, three chances.

**3. The leak specs/029 just closed.** Deleting a product must take its variants' images. Adding
them without extending that cleanup reintroduces the same defect in the same month, which is why
the test comes first and must be seen red.

## Phases

**Phase 1 - the column and the key.** Two columns on `ProductVariant`, the EF configuration with the
same CHECK the product has, a migration, and `ProductImageKey.ForVariant`. A test that a product and
its first variant produce **different** keys for the same instant.

**Phase 2 - the writes.** Upload, replace, remove - handlers modelled on the product ones, guarded
by `SellerOwnership`. Controller routes with `"Seller,Admin"`.

**Phase 3 - the read.** `VariantResponse.ImageUrl`, resolved server-side to the variant's or the
product's. A test for each branch.

**Phase 4 - the cleanup.** `DeleteProductCommandHandler` collects every variant key beside the
product's. A test seen **red** first.

**Phase 5 - the storefront.** The chooser drives the picture; the listing card does not change.
Seller upload per variant on `/shop/products/:id`. Vitest tests.

**Phase 6 - end to end.** Two sellers for SC-002; a real upload and a screenshot of the chooser
swapping; `verify-saga.sh`; Bruno.

**Phase 7 - say so.** CLAUDE.md, and the specs/019 paragraph which currently says images hang on
products.

## Verification

- Catalog 115 to about 123, client 38 to about 42.
- The delete test **must be seen failing** before Phase 4's change.
- SC-002 against the API with two real tokens, not the page.
- A screenshot per variant, because "the picture changed" is not a thing a unit test can see.
