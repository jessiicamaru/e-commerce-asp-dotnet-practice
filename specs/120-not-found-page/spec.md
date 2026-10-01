# Feature Specification: A real not-found page

**Feature Branch**: `fix/243-not-found-page` | **Created**: 2026-10-01 | **Issue**: #243

**Status**: Draft

**Input**: Issue #243 - "the not-found page is one untranslated line" - found in the screen review of 2026-10-01.

## Why

A mistyped address, an old link or a product taken down sends a shopper to the not-found page. It was the words "Not found." at the top left - in English for a Vietnamese reader, with no way on. A dead end is where a shop loses the person who was looking for something.

## User Scenarios & Testing *(mandatory)*

### US1 - A lost shopper finds their way back (Priority: P1)



**Acceptance Scenarios**:

1. **Given** an address no route matches, **Then** the page says the page was not found and why that happens, in the reader's language, with a link back to the shop and a search box.
2. **Given** a search typed there, **Then** the catalogue opens with that search (`/?q=`), as the header's does.
3. **Given** Vietnamese chosen, **Then** every word on the page is Vietnamese.

### Edge Cases

- The page sits inside the normal frame, so the header and its search stay too.
- Only unknown addresses: a known page's own 404 (an order, a product, a shop) keeps its own message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: An unknown address shows a translated heading, a sentence, a search form and a link to the shop.
- **FR-002**: Searching from it opens the catalogue with that search.
- **FR-003**: Tests cover both languages and the search.

## Success Criteria *(mandatory)*

- **SC-001**: No unknown address shows an untranslated or bare page (tested in both languages).

## Assumptions

- The unused `common.notFound` string becomes the page's group of keys.
