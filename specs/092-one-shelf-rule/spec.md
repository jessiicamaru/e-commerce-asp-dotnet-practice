# Feature Specification: One rule for "off the shelf"

**Feature Branch**: `092-one-shelf-rule` | **Created**: 2026-09-27 | **Issue**: #185

**Status**: Merged (#191, 2026-09-27)

**Input**: Issue #185 - "off the shelf is IsListed for reads but IsListed and IsActive for writes".

## Why

Two rules decided whether a product is off the shelf, and they disagreed:

| Asked by | Rule |
| :-- | :-- |
| The public lookup, its photographs, reviews and questions (`ProductReview.MaySee`, `ProductImageKey.MayServe`) | `IsListed` - approved |
| The public listing and search (`ProductRepository`, `listedOnly`) | `ReviewStatus = Approved` |
| Recording a view (specs/047, 086) | `IsListed` |
| Writing a review (specs/085), asking a question (specs/076), saving (specs/075), telling savers | `IsListed && IsActive` |
| Pricing and checkout (`CatalogPricing`, `ProductVariant.Sellable`) | `IsListed && IsActive` |

So a product **withdrawn** (`IsActive = false`) but still approved could be opened by anybody - with its photograph,
its reviews and its questions - and listed in search, while nobody could buy it, review it, ask about it or save it.

This record also established how a product becomes withdrawn: **no command sets `Product.IsActive`**. It is `true` by
default and nothing writes it (research D2). The state is reachable only through an earlier image, a data fix by hand,
or a future "withdraw" feature - which is exactly when a split rule is found by a shopper rather than a test.

## User Scenarios & Testing *(mandatory)*

### US1 - A withdrawn product is off the shelf everywhere (Priority: P1)

A product that is approved but withdrawn is treated exactly like one that is not approved: the public lookup, the
listing and search, its photographs (without the image's key), its reviews and its questions are the same 404 /
absence for the public; its seller and staff still see all of it.

**Why this priority**: It is the defect: one product, two answers to "is it on the shelf".

**Independent Test**: List a product with a photograph, set `IsActive = false` in the database, and as a shopper:
the lookup is null, search does not find it, the photograph is refused without its key (and served privately with
it), reviews and questions are 404. As a moderator, the lookup still answers.

**Acceptance Scenarios**:

1. **Given** an approved, withdrawn product, **When** a shopper opens it, **Then** it is not found.
2. **Given** the same, **When** a shopper searches or browses, **Then** it is not listed.
3. **Given** the same, **When** a request asks for its photograph without the key, **Then** nothing is served; with
   the key, it is served but never marked for a shared cache.
4. **Given** the same, **When** a shopper asks for its reviews or questions, **Then** 404.
5. **Given** the same, **When** its seller or staff open it, **Then** they see it as before.

---

### US2 - One definition, in one place (Priority: P1)

"On the shelf" is one property of the product - approved **and** not withdrawn - and every read, write and sale asks
it. `IsListed` (approved) stays as the review state it names, and is read nowhere else.

**Why this priority**: The defect came from two call sites writing the rule differently; one definition is what stops
the next caller doing the same.

**Independent Test**: A mutation that drops `IsActive` from the definition turns the US1 test red, and so does one that
reverts only the lookup, or only the listing.

**Acceptance Scenarios**:

1. **Given** the code, **Then** every public read, shopper write, view, save, saved-product notice and both pricing
   paths decide through `Product.OnShelf` (or, in SQL, its spelled-out equivalent in one place).

### Edge Cases

- **A withdrawn product somebody saved.** It stays in their list as "no longer available" (specs/075), unchanged.
- **Its photograph cached while on sale.** Served only with the image's own key once withdrawn, as for a take-down
  (specs/081).
- **A withdrawn product's views.** Not counted, as for an unapproved one (views used `IsListed` alone before).
- **A variant deactivated.** Unchanged: that is the variant's `IsActive`, which governs sellability of that variant, not
  whether the product is on the shelf.
- **Moderation queues.** Unchanged - staff see products by review status, whatever `IsActive` is.
- **An earlier image after a rollback.** It reads `IsListed` alone and would show a withdrawn product; no image today
  writes `IsActive = false`, so the window is the same as today's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Product.OnShelf => IsListed && IsActive` is the single definition of "on the shelf".
- **FR-002**: `ProductReview.MaySee`, `ProductImageKey.MayServe`, the image queries' public flag, the listing filter,
  the view recorder, reviews, questions, saving, the saved list's availability, saved-product notices,
  `CatalogPricing` and `ProductVariant.Sellable` use it.
- **FR-003**: The listing query, which runs in SQL, spells it out as `ReviewStatus = Approved AND IsActive`, with a
  comment naming `OnShelf`.
- **FR-004**: The seller and staff keep seeing a withdrawn product (their existing exceptions in `MaySee`).
- **FR-005**: No migration, no contract change, no new endpoint.

### Key Entities

- **Product** - `ReviewStatus` (specs/045) and `IsActive` (since the start); `IsListed` and `OnShelf` are derived, not
  stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `A_withdrawn_product_is_off_the_shelf_for_reads_as_it_is_for_writes` fails before the fix and passes
  after.
- **SC-002**: Three mutations - the definition without `IsActive`, the listing without it, `MaySee` back on `IsListed` -
  each turn it red.
- **SC-003**: `Ecommerce.Catalog.Tests` passes in full.
- **SC-004**: `IsListed` is referenced only by `OnShelf` and the EF mapping.

## Decision

**A withdrawn product is the same 404 as one taken down - not a page marked "no longer sold".** The issue asked which.
The stricter rule was chosen because:

- no route withdraws a product today, so no shopper has a link to a withdrawn page to lose;
- it is the rule writes and checkout already used, so only reads change, and they change toward what the constitution
  prefers (fail closed);
- the saved list already says "no longer available" for anything off the shelf, without needing the page;
- a future "withdraw" feature that wants a "no longer sold" page changes `OnShelf` in one place, with this record
  saying why it is strict today.

Recorded as decided on the user's behalf ([research.md](research.md) D1).

## Assumptions

- `Product.IsActive` means "withdrawn by its seller or the shop" when false - the meaning the write paths gave it.
- Staff and the seller keep their access to anything off the shelf, as for a take-down (specs/045, 081).

## Out of scope

- A command to withdraw or restore a product.
- A "no longer sold" product page.
