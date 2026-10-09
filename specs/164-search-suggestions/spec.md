# Feature Specification: Search suggestions while typing

**Feature Branch**: `feature/376-search-suggestions`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #376
**Input**: "While typing, a dropdown under the search box offers a few products and matching categories; choosing one
opens it; Enter still searches; keyboard and screen readers work."

## Context

The search box searches only on Enter (specs/017): nothing is offered while typing, a misspelled or half-remembered
name lands on an empty page, and categories are never offered. Search itself is diacritic-insensitive and indexed
(specs/074), so the expensive part exists. Found in the review of 2026-10-09.

## User Scenarios & Testing

### User Story 1 - Suggestions while typing (Priority: P1)

After two characters, a dropdown under the search box offers up to 6 products (picture, name, price) and up to 4
categories, matched the way search matches.

**Why this priority**: it is the feature.

**Independent Test**: type "may anh" - "Máy ảnh" categories and cameras are offered; type "son" - Sony products.

**Acceptance Scenarios**:

1. **Given** two characters or more, **When** the shopper pauses, **Then** products and categories matching them are
   offered, in the reader's language and currency, only products on the shelf.
2. **Given** a term typed without diacritics, **Then** names with them match ("may anh" finds "Máy ảnh").
3. **Given** one character, or nothing matching, **Then** no dropdown (or an empty one saying so) is shown.

---

### User Story 2 - Choosing (Priority: P1)

Choosing a product opens it; choosing a category opens the listing filtered to it; Enter with nothing highlighted
searches exactly as today; up/down move, Escape closes, and a screen reader announces the options.

**Why this priority**: suggestions that cannot be chosen by keyboard are half a feature.

**Independent Test**: type "son", press down twice and Enter: that product opens. Type "son" and Enter: the search page.

**Acceptance Scenarios**:

1. **Given** suggestions, **When** a product is chosen (click or Enter on it), **Then** its page opens.
2. **Given** suggestions, **When** a category is chosen, **Then** the listing opens filtered to it.
3. **Given** nothing highlighted, **When** Enter is pressed, **Then** the search runs as before.
4. **Given** suggestions, **When** Escape is pressed or the box loses focus, **Then** they close.

### Edge Cases

- **Typing fast**: one request per pause (200 ms), never one per key; an answer for an older term is never shown over a
  newer one.
- **Signed in or out**: the same - suggestions are anonymous and cacheable (specs/157).

## Requirements

### Functional Requirements

- **FR-001**: `GET /api/products/suggest?q=` (anonymous, cached) MUST answer up to 6 products on the shelf matched by the
  catalogue's own search, and up to 4 categories - every category the categories list shows - whose name in the
  reader's language or original name contains the term ignoring case and diacritics.
- **FR-002**: The term MUST be 2-100 characters after trimming; otherwise 400.
- **FR-003**: A product suggestion MUST carry its id, name in the reader's language, picture, "from" price and currency;
  a category its id and name.
- **FR-004**: The storefront MUST suggest after 2 characters and a 200 ms pause, and offer keyboard (up, down, Enter,
  Escape) and screen-reader use (combobox and listbox roles); Enter without a highlighted option searches as today.

## Success Criteria

- **SC-001**: A term without diacritics finds products and categories with them (tested on PostgreSQL).
- **SC-002**: Nothing off the shelf, at most 6 products and 4 categories (tested).
- **SC-003**: In a browser, typing shows suggestions, the keyboard chooses one, and Enter still searches.
- **SC-004**: Bruno covers the read and its refusal; `docs/reference` regenerated.

## Assumptions

- Product ranking is the listing's default for a search; a relevance score is out of scope.
- Categories are few (tens), so they are matched in memory from the cached list rather than by an index.
