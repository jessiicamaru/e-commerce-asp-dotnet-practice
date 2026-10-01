# Feature Specification: Notice wording is edited like the emails

**Feature Branch**: `feat/250-notice-wording-layout` | **Created**: 2026-10-01 | **Issue**: #250

**Status**: Draft

**Input**: Issue #250 - "notice wording is edited like the emails" - found in the screen review of 2026-10-01.

## Why

`/admin/notifications` listed every sentence of every notice in every language as its own card - about 110 cards, 7,000px - each titled with the kind's code (`AccountBanned`, `ParcelAutoDelivered`). Finding the one sentence to change meant scrolling or knowing its code. `/admin/emails` had already solved the same problem: pick what, pick the language, edit one thing.

## User Scenarios & Testing *(mandatory)*

### US1 - An administrator picks a notice by name and edits it in one language (Priority: P2)



**Acceptance Scenarios**:

1. **Given** `/admin/notifications`, **Then** the notices are a list of readable names ("Account banned", "Parcel taken as delivered") with a search box and a mark on the edited ones, a language switch, and the chosen notice's sentences - each plural form - with their editors open.
2. **Given** a notice and a language chosen, **Then** the address says so (`?kind=&lang=`), so it can be shared and survives a reload.
3. **Given** a kind the storefront has no name for yet, **Then** its code is shown rather than nothing - and a test fails until it has names in both languages.

### Edge Cases

- The editor itself (placeholders, preview, versions, reset) is unchanged (specs/078); only how one is reached.
- The search matches the readable name and the code, in the reader's language.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The page shows a kind list (name, edited mark, search), a language switch and the chosen kind's sentences with their editors, at one time.
- **FR-002**: The choice is in the address: `?kind=` and `?lang=`.
- **FR-003**: Every kind in `notification-kinds.json` has a name in `admin.json` (en, vi), held by a test.

## Success Criteria *(mandatory)*

- **SC-001**: The page is about one screen tall instead of ~7,000px (checked in a browser).
- **SC-002**: A kind without a name fails a test.

## Assumptions

- 38 kinds today; the list scrolls inside its column when it grows.
