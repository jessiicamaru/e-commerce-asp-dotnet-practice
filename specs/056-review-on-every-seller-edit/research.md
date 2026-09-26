# Research: Review on every seller edit

> Written on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

---

## D1 - Adding a variant is an edit of what a shopper reads

**Decision**: `AddProductVariantCommandHandler` calls `AfterSellerEditAsync`.

**Rationale**: A new variant puts new option words and a new shape on an approved listing. Specs/045's decision,
made with the user, is "what a shopper sees" versus "prices and stock". `docs/features/catalog.md` said new
variants did not send a product back "as decided with the user"; the pull request checked specs/045, found
variants not mentioned, and concluded the doc had described the code as it stood rather than a decision.

**Alternatives considered**:

- **Exempt new variants, as the doc said.** Rejected on that reading of specs/045; the pull request notes it
  would be a one-line revert if the user wants it after all. Whether the user was asked about this reading is not
  recorded.

---

## D2 - Translating an option is too, and it is audited

**Decision**: `SetOptionTranslationCommandHandler` records `OptionTranslated` (category Catalog, subject the
product, before `{Language, Name, Value}` or null, after the new values) and then calls `AfterSellerEditAsync`.

**Rationale**: An option's words are shown on the product page and frozen onto order lines. Every other
translation edit was already audited; this one was not.

**Alternatives considered**: none recorded.

---

## D3 - The guard stays inside the rule

**Decision**: Neither handler checks the status, the seller or the role; `AfterSellerEditAsync` returns early
unless the product is `Approved`, has a `SellerId`, and the caller is not an administrator.

**Rationale**: One place decides, as for the six handlers before. A handler that repeated the guard would be a
second place to get it wrong.

**Alternatives considered**: none recorded.
