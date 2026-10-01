# Feature Specification: A seller edits what they listed

**Feature Branch**: `feat/240-seller-edits-listing` | **Created**: 2026-10-01 | **Issue**: #240

**Status**: Draft

**Input**: Issue #240 - "a seller edits what they listed - details, translations and variants" - found in the screen review of 2026-10-01.

## Why

A seller's product page edited the photograph, prices and stock - nothing else. A typo in the name, a description to improve, the wrong category, an English version for the shop's English readers, a second kit: none of it could be done, while the page warned that editing the name or description would send the product back to review, and the new-product form promised "variants, a second currency and photographs come next, on the listing's page". The translation and variant endpoints existed and nothing called them; the product's own name, description and category had no endpoint at all.

## User Scenarios & Testing *(mandatory)*

### US1 - A seller corrects the name, description and category (Priority: P1)



**Acceptance Scenarios**:

1. **Given** a seller's own product, **When** they change its name, description or category on its page, **Then** the shop shows the new text and category.
2. **Given** that product was approved, **Then** it goes back to review and off the shelf, as the page warns (specs/045) - also for a category change, which moves it in front of other shoppers.
3. **Given** another seller's product, **Then** the change is 404, as every write (specs/027); an administrator may edit any product and nothing goes back to review.
4. **Given** a category that does not exist, **Then** 400 on `CategoryId`.

---

### US2 - A seller writes the product in each language (Priority: P1)



**Acceptance Scenarios**:

1. **Given** the shop speaks Vietnamese (the default) and English, **Then** the page edits the default text in the details and offers an English name and description, saying when there is none yet and English readers see the Vietnamese.
2. **Given** an English text saved, **Then** English readers see it; removed, they see the default again.

---

### US3 - A seller adds a variant (Priority: P1)



**Acceptance Scenarios**:

1. **Given** a product, **When** the seller adds a variant with a SKU, a price in the default currency and its options, **Then** it appears in the variants list with no stock, ready for prices and stock.
2. **Given** a SKU already used, **Then** the server's refusal is shown in its words.

---

### US4 - Listing a product ends where the form ends (Priority: P3)



**Acceptance Scenarios**:

1. **Given** the new-product form, **Then** "List it" is at the end of the form, and the hint promises only what the next page does.

### Edge Cases

- The page is read in the default language for the details (whatever the seller browses in) and in each other language for its translation - a response carries one language's text (specs/021), like prices per currency.
- Nothing changed (the same name, description and category saved again) sends nothing back to review.
- Option translations (`Kit: Body only` → `Bộ: Chỉ thân máy`) keep their endpoint; their editor is not part of this - the issue asks for the product's text, and option values are mostly shared vocabulary.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `PUT /api/products/{id}` (Seller, Admin) sets the product's default-language name and description and its category; owner or administrator only (404 otherwise); validated like creation; audited `ProductDetailsEdited`; calls `ProductReview.AfterSellerEditAsync` when anything changed.
- **FR-002**: The product lookup carries `translatedLanguages`: the languages with a translation row.
- **FR-003**: The seller's product page has a details card, a translations card per non-default language (save, remove) and an add-variant form (SKU, price, options).
- **FR-004**: The new-product form's submit is at the end of the form; its hint no longer promises what does not exist.

## Success Criteria *(mandatory)*

- **SC-001**: Every field a seller entered when listing can be changed afterwards (tested end to end in Bruno).
- **SC-002**: An approved product edited by its seller is back in review (tested against PostgreSQL).

## Assumptions

- The shop's languages are the configured ones (vi default, en); the page reads the list from the client's `LANGUAGES`, as the language picker does.
