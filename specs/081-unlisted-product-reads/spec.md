# Feature Specification: What hangs on a product off the shelf

> Completed on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature Branch**: `081-unlisted-product-reads` | **Created**: 2026-09-26 | **Status**: Merged (#169, 2026-09-26) | **Issue**: #166 (closes it)

**Input**: Issue #166 - a product taken down answered 404, while its image, reviews and questions still answered 200
to anyone who knew its id.

## Why

A product that is not on sale (pending, rejected or taken down) is the public lookup's **404** (specs/045). Its
**image, reviews and questions were still served to anyone who knew the id**. Verified against the running stack:
taken down, the product answered 404, while its image, reviews and questions answered 200.

The probe from the issue, anonymous, after a take-down (from the pull request):

| Request | Before | After |
| :-- | :-- | :-- |
| `GET /api/products/{id}` | 404 | 404 |
| `GET /api/products/{id}/image` | **200** | 404 |
| `GET /api/products/{id}/reviews` | **200** | 404 |
| `GET /api/products/{id}/questions` | **200** | 404 |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A product taken down shows nothing to the public (Priority: P1)

A moderator takes a product down (a counterfeit, say). A stranger who kept its id, or a search engine that indexed
its pages, asks for its reviews, its questions and its photograph. All three answer exactly as the product itself
does: not found.

**Why this priority**: This is the defect the issue reports. Taking a product down is a moderation decision, and
everything that hangs on the product undid it for anybody holding the id. It is also the cheapest part: reviews
and questions only have to ask the rule the product lookup already asks.

**Independent Test**: Take a listed product with a review, a question and a photograph down; ask for each
anonymously; every answer is 404, the same as for an id that never existed.

**Acceptance Scenarios**:

1. **Given** a product taken down, **When** an anonymous caller asks for its reviews, **Then** the answer is 404
   `Product not found.`, the same as for a product that does not exist.
2. **Given** a product taken down, **When** an anonymous caller asks for its questions, **Then** the answer is the
   same 404.
3. **Given** a product taken down, **When** an anonymous caller asks for its image with no key, or with any key
   but the image's own, **Then** the answer is 404.
4. **Given** a product on sale, **When** anybody asks for its reviews and questions, **Then** they are served as
   before.

---

### User Story 2 - The seller and staff still see what hangs on it (Priority: P2)

The seller of a product waiting for review, or taken down, opens it in their shop console; a moderator opens it in
the queue. They see its photograph, its reviews and its questions, as they see the product itself.

**Why this priority**: Without it the fix for User Story 1 breaks the seller's and the moderator's pages: a seller
could not see the photograph of the product they are waiting on, and a moderator could not judge one. It comes
second because it only matters once the public is refused.

**Independent Test**: As the seller of a pending product with a photograph, read the product and follow its
`imageUrl`; the image is served. As a stranger, follow the same product's image address without the key; 404.

**Acceptance Scenarios**:

1. **Given** a seller's product waiting for review, **When** its seller or a member of staff reads the product,
   **Then** the response carries an `imageUrl` with the image's key (`&k=`), and that address serves the image.
2. **Given** the same product, **When** a stranger asks for its image without the key, **Then** the answer is 404.
3. **Given** a product taken down, **When** its seller reads its questions, **Then** they are served (Bruno
   `seller/its seller still reads its questions`).
4. **Given** an image of a product off the shelf is served to a keyed address, **When** the response is sent,
   **Then** it carries `Cache-Control: private, no-cache`, so no shared cache keeps it.

---

### User Story 3 - A replaced photograph retires the old address (Priority: P3)

A seller replaces the photograph of a product that is off the shelf. An address handed out for the old photograph
no longer opens anything new.

**Why this priority**: The key is a capability; replacing the image is the one way to revoke an address somebody
already holds. It matters less than the first two because the holder has already seen the old photograph.

**Independent Test**: Read the product's `imageUrl`, replace the image, then ask for the image with the old key
while the product is off the shelf; 404. The new address serves the new image.

**Acceptance Scenarios**:

1. **Given** an image with key A, **When** it is replaced, **Then** the image gets a new key B, and while the
   product is off the shelf an address carrying A opens nothing.
2. **Given** a variant with its own photograph, **When** the product is off the shelf, **Then** the variant's
   image follows the same rule as the product's.
3. **Given** an image removed, **When** its row is switched, **Then** its key is cleared with it.

---

### Edge Cases

- **Addresses already cached while on sale.** An image address from before this feature carries no key. While the
  product is on sale it keeps working, with or without the key, so every cached page still shows its pictures.
- **A product that does not exist.** Reviews and questions of an unknown id are the same 404 with the same words,
  so the answer confirms nothing about whether the id is real.
- **An empty page instead of a 404.** Rejected: an empty page of reviews would confirm the product exists.
- **An address already handed out.** It keeps opening that image until the image is replaced. The key is not an
  expiring signature. Anybody holding it has seen the photograph already. Recorded as a known limit.
- **A variant image whose product is off the shelf.** The product's listing decides, the variant's own key opens
  it.
- **An image stored before this feature.** The migration gives it a key, otherwise its seller could not see it
  while the product waits.
- **An inactive but approved product.** The rule is `IsListed` (review status `Approved`), the same as the public
  lookup's; `IsActive` is not part of `ProductReview.MaySee`. Not changed by this feature.
- **A rollback to an earlier image.** The earlier image ignores the new columns and serves images as before; the
  migration only adds.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-004** The key is written by the same guarded statement that switches the image (specs/019), so the row
  never names an image with another image's key; removing an image clears its key.
- **FR-005** The storefront needs no change: it already uses the `imageUrl` the server returns.

### Key Entities

- **Product image**: the product's photograph, named by `ImageUpdatedAt` (the version, `v`) and now also by
  `ImageAccessKey` (`k`), both on the `products` row.
- **Variant image**: a variant's own photograph (specs/032), with the same two values on the `product_variants`
  row.
- **Review and question**: unchanged; what changed is who may read the list of a product off the shelf.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Anonymous, after a take-down, the image, reviews and questions of the product all answer 404, where
  before all three answered 200 (the issue's probe, re-run against the rebuilt stack).
- **SC-002**: The seller and staff still read the photograph and questions of a product off the shelf: 100% of the
  seller's and moderator's views keep working (Bruno `seller/its seller still reads its questions`; the test for a
  waiting product's photograph).
- **SC-003**: Every image address already cached for a product on sale keeps working: an image on sale is served
  with and without the key.
- **SC-004**: A replaced image's old key opens nothing while the product is off the shelf.
- **SC-005**: Each of five mutations of the rule turns `UnlistedProductReadsTests` red (from the pull request).

## Assumptions

- The public lookup's rule (`ProductReview.MaySee`: on the shelf, everybody; otherwise its seller and staff) is the
  right rule for what hangs on a product, and is not redesigned here.
- An image's photograph is not secret once someone has legitimately seen it; revoking an address somebody holds is
  worth nothing more than replacing the image.
- Catalog still streams every image itself (specs/079); there is no CDN in front of it whose cache would need
  purging.

## Decisions

- **A capability address, not a signed URL.** The key lives on the row and is written by the guarded statement
  that switches the image, so it needs no secret and no expiry, and every Catalog instance agrees on it. The one
  thing it cannot do is revoke an address somebody already has without replacing the image. For a photograph they
  have already seen, that is accepted.
- **Reviews and questions answer 404, not an empty page.** An empty page would confirm the product exists.

The full reasoning, with the rejected alternatives, is in [research.md](./research.md).

## Out of scope

- Revoking an image address without replacing the image.
- A CDN or presigned bucket URLs (specs/079 left them out too).
- Writing a review of a product off the shelf - closed separately in
  [specs/085](../085-unlisted-review-writes/) (#174).
