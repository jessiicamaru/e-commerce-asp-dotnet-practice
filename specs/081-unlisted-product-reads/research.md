# Research: What hangs on a product off the shelf

> Written on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-26

Five decisions. The first two are recorded in the spec's Decisions and in
[decisions.md row 63](../../docs/project/decisions.md); the rest are read from the code. Who decided each is not
recorded beyond the pull request.

---

## D1 - Reviews and questions ask the public lookup's rule

**Decision**: `GetProductReviewsQuery` and `GetProductQuestionsQuery` load the product and throw
`NotFoundException("Product not found.")` unless it exists and `ProductReview.MaySee(product, currentUser)` - the
rule `GetProductByIdQueryHandler` already asks (specs/045): on the shelf, everybody; otherwise its seller, an
administrator or a moderator.

**Rationale**: What hangs on a product should answer like the product. Calling the one rule rather than copying it
keeps the three reads from drifting apart when the rule changes. The same words for "off the shelf" and "no such
id" mean the answer confirms nothing.

**Alternatives considered**:

- **An empty page for a product off the shelf.** Rejected: an empty page confirms the product exists (the spec's
  second decision).
- **A filter inside the review and question repositories.** Rejected: it would be a second copy of the listing
  rule, in SQL, that the next change to `MaySee` would miss.

---

## D2 - Images need a key in the address, because an `<img>` carries no token

**Decision**: every image, product and variant, gets an `ImageAccessKey` (`uuid`). The address the server hands out
is `/api/products/{id}/image?v={version}&k={key:N}` (and the variant's equivalent). `ProductImageKey.MayServe` is
`product.IsListed || (imageKey is not null && imageKey == key)`. The key is `Guid.NewGuid()`.

**Rationale**: the storefront keeps its access token in memory (CLAUDE.md), and a browser's image request sends no
`Authorization` header, so the image endpoint cannot learn who is asking. What it can check is whether the caller
holds an address only a permitted reader was given: the address appears only in responses that passed `MaySee`.
`Guid.NewGuid()` is random; ADR-001's `CreateVersion7()` is for primary keys and puts a timestamp in the first
bits, which is the wrong property for something that must not be guessed.

**Alternatives considered**:

- **A signed, expiring URL.** Rejected (decisions row 63): it needs a secret shared by every Catalog instance and
  an expiry to choose, and a cached page would break when it expires. A key on the row needs no secret, and every
  instance agrees on it because it reads the same row.
- **Sending the token on image requests** (fetching images through JavaScript as blobs). Rejected by omission: it
  would change every `<img>` in the storefront, which the feature did not need to touch.
- **A cookie for images.** Not recorded as considered.

---

## D3 - On sale, any address still works

**Decision**: while the product is listed, the image is served with or without the key, and the cache header is
unchanged (`public, max-age=31536000, immutable` when `v` is current, else `no-cache`).

**Rationale**: every page cached before the feature holds addresses with no `k`. Requiring the key for listed
products would have broken every cached product picture for nothing, since a listed product's photograph is public
anyway.

**Alternatives considered**:

- **Require the key always.** Rejected for the reason above.

---

## D4 - Off the shelf, never a shared cache

**Decision**: `ProductImage.Public` is `product.IsListed`; when false the controller's `CacheFor` writes
`Cache-Control: private, no-cache`.

**Rationale**: a keyed image of a product off the shelf is served to one permitted reader. With the usual
`public, immutable` header a shared cache between the browser and the gateway could keep it and hand it to the next
caller of the same address.

**Alternatives considered**:

- **`no-store`.** Not recorded as considered; `private, no-cache` lets the reader's own browser revalidate it.

---

## D5 - A new image, a new key; the key moves with the guarded switch

**Decision**: `TrySetImageAsync` and `TrySetVariantImageAsync` gained an `accessKey` argument and set it in the same
`ExecuteUpdateAsync ... WHERE "ImageUpdatedAt" = @expected` that switches the image. Upload passes a new Guid;
remove passes null. The migration `AddImageAccessKeys` adds the two nullable columns and runs
`UPDATE ... SET "ImageAccessKey" = gen_random_uuid() WHERE "ImageUpdatedAt" IS NOT NULL` on both tables.

**Rationale**: replacing the image is the one way to revoke an address somebody holds, so the key must change with
the image and with nothing else. Writing both in one statement means no reader ever sees a new image with an old
key. The backfill is needed because without a key the image of a product already off the shelf could not be shown
even to its own seller.

**Alternatives considered**:

- **A key per product rather than per image.** Rejected: replacing the photograph would not retire the old
  address.
- **Leaving existing images without a key.** Rejected for the reason above.

---

## Known limit

An address already handed out keeps opening its image until the image is replaced. Anybody holding it has seen the
photograph already (the pull request's "Known limit"; `docs/features/catalog.md`, Known limits).
