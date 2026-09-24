# Feature Specification: Review on every seller edit

**Feature Branch**: `056-review-on-every-seller-edit` | **Created**: 2026-09-24 | **Issue**: #126

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

## User Scenarios

### US1 - Every seller edit of what a shopper reads is reviewed (P1)

**Acceptance**
1. A seller translating an option of their approved product sends it back to `Pending`, off the shelf.
2. A seller adding a variant to their approved product sends it back to `Pending`.
3. Staff doing either does not: moderation is their job, as for the other six edits.
4. A seller's product already `Pending` or `Rejected` is unaffected.

### US2 - It is on the record (P2)

**Acceptance**
1. Translating an option records an audit entry, `OptionTranslated`, under Catalog.
2. When the translation sends the product back, `ProductSentForReview` is recorded too, as for every
   other edit.

## Requirements

- **FR-001**: Both handlers call `ProductReview.AfterSellerEditAsync` before their one save.
- **FR-002**: The option translation records `OptionTranslated`, with the language, name and value.
