# Feature Specification: What hangs on a product off the shelf

**Feature Branch**: `081-unlisted-product-reads` | **Created**: 2026-09-26 | **Issue**: #166 (closes it)

## Why

A product that is not on sale (pending, rejected or taken down) is the public lookup's **404** (specs/045). Its
**image, reviews and questions were still served to anyone who knew the id**. Verified against the running stack:
taken down, the product answered 404, while its image, reviews and questions answered 200.

## Requirements

- **FR-001** Reviews and questions of a product that is not listed are a **404**, except to its seller and staff.
  - This is the public lookup's own rule, `ProductReview.MaySee`, not a copy of it.
  - A product that does not exist is the same 404.
- **FR-002** Images need more than that, because **a browser's image request carries no token**. The access token
  lives in memory, and an `<img>` sends no `Authorization` header. So every image (product and variant) gets an
  unguessable **`ImageAccessKey`**, a Guid that is new with every image. Its address carries it: `?v=...&k=...`.
  - **On sale**: the image is served to anyone, with or without the key. Addresses already cached keep working.
  - **Off the shelf**: the image is served only to an address carrying the image's own key, and never with a
    public cache header (`private, no-cache`).
  - Only a response its reader may see carries the address, so only its seller and staff can open the image of a
    product off the shelf.
  - A new image gets a new key, so an address handed out for the old image opens nothing.
- **FR-003** The migration only adds: the columns, plus a key for every image already stored.

## Decisions

- **A capability address, not a signed URL.** The key lives on the row and is written by the guarded statement
  that switches the image, so it needs no secret and no expiry, and every Catalog instance agrees on it. The one
  thing it cannot do is revoke an address somebody already has without replacing the image. For a photograph they
  have already seen, that is accepted.
- **Reviews and questions answer 404, not an empty page.** An empty page would confirm the product exists.
