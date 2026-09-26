# Feature Specification: Review on every seller edit

> Completed on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature Branch**: `056-review-on-every-seller-edit` | **Created**: 2026-09-24 | **Issue**: #126

**Status**: Merged (#139, 2026-09-24)

**Input**: Issue #126 - two seller edits of what a shopper reads skip `ProductReview.AfterSellerEditAsync`, so an
approved product can be changed into anything and stay on sale.

## Why

Specs/045 made a seller's change to an approved product send it back to review and off the shelf,
decided with the user, for everything a shopper **reads**: the name, the description and any photograph.
Prices and stock do not send it back. The rule is `ProductReview.AfterSellerEditAsync`, called before
the one save in six handlers. CLAUDE.md already warned: "a seventh edit of what a shopper reads must call
it too".

Two do not:
- **Translating a variant option.** `Kit: Body only` becomes whatever the seller writes, and it is shown
  on the product page and frozen onto order lines.
- **Adding a variant.** It adds new option words, and a new shape, to an approved listing.

So a seller could get a product approved, then add a variant or reword an option into anything, and it
stayed on sale unreviewed. Translating an option also records no audit entry, while every other
translation edit does.

## User Scenarios & Testing *(mandatory)*

### US1 - Every seller edit of what a shopper reads is reviewed (Priority: P1)

A seller changing the words or shapes a shopper sees on an approved product sends it back to the moderators,
whichever screen they changed it from.

**Why this priority**: It closes the hole in specs/045's rule; without it moderation of seller listings can be
walked around by two ordinary edits.

**Independent Test**: Approve a seller's product; as the seller add a variant (or translate an option); read the
product's review status.

**Acceptance Scenarios**:

1. A seller translating an option of their approved product sends it back to `Pending`, off the shelf.
2. A seller adding a variant to their approved product sends it back to `Pending`.
3. Staff doing either does not: moderation is their job, as for the other six edits.
4. A seller's product already `Pending` or `Rejected` is unaffected.

---

### US2 - It is on the record (Priority: P2)

**Why this priority**: An edit that sends a product back should be traceable, as every other translation edit
already is; it follows US1.

**Independent Test**: Translate an option as the seller and read the audit entries for the product.

**Acceptance Scenarios**:

1. Translating an option records an audit entry, `OptionTranslated`, under Catalog.
2. When the translation sends the product back, `ProductSentForReview` is recorded too, as for every
   other edit.

### Edge Cases

- **The shop's own product** (`SellerId` null): never sent back, whoever edits it.
- **"Staff" here means an administrator.** `AfterSellerEditAsync` exempts `StaffRoles.Admin`; the endpoints are
  `Seller,Admin`, so a moderator cannot make these edits at all.
- **A product sent back twice.** Already `Pending`, so the second edit changes nothing and records no second
  `ProductSentForReview`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Both handlers call `ProductReview.AfterSellerEditAsync` before their one save.
- **FR-002**: The option translation records `OptionTranslated`, with the language, name and value.
- **FR-003**: The rule itself is unchanged: approved products only, sellers' products only, never for an
  administrator - decided inside `AfterSellerEditAsync`, not repeated by the handlers.

### Key Entities

- **Product review status** (`products.ReviewStatus`, specs/045): `Approved` / `Pending` / `Rejected`; `Pending`
  takes a product off the shelf.
- **Variant option translation** (`variant_option_translations`, specs/021): an option's name and value in one
  language.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After either edit by the seller, the approved product reads `Pending` and is not listed - verified by
  two tests that failed before the fix with `Approved`.
- **SC-002**: The same edits by an administrator leave the product `Approved`.
- **SC-003**: An option translation produces one `OptionTranslated` entry carrying the new words, and one
  `ProductSentForReview` when it sends the product back.

## Assumptions

- Specs/045's decision covers what a shopper sees versus prices and stock; new variants are what a shopper sees.
  The pull request records that `docs/features/catalog.md` had said new variants were exempt "as decided with the
  user", checked specs/045, found no such decision, and corrected the doc - with the note that exempting new
  variants again would be a one-line revert.

## Out of scope

- Any change to what does *not* send a product back (prices, stock, activating or deactivating a variant).
