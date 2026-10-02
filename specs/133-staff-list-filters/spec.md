# Feature Specification: Long staff lists can be searched and filtered

**Feature Branch**: `feat/249-staff-list-filters` | **Created**: 2026-10-02 | **Issue**: #249

**Status**: Draft

**Input**: Issue #249 - "long staff lists can be searched and filtered" - found in the screen review of 2026-10-01.

## Why

The platform's vouchers were 58 tall cards with no way to find one; the people list ran to 34 pages with a name search and nothing else - no way to see the moderators, or who is locked; the categories were one unsearched page; Find an order printed a failure as the server's raw text ("Insufficient stock for product 01a0f357-…") and its state tabs gave no idea how many orders each held.

## User Scenarios & Testing *(mandatory)*

### US1 - Vouchers by code, name and state (Priority: P1)



**Acceptance Scenarios**:

1. **Given** the vouchers page (an administrator's or a seller's), **When** a code or a name is typed, **Then** only vouchers whose code or name contains it are listed.
2. **Given** the state tabs All / Active / Ended / Disabled, **Then** the list shows that state, with the count on each tab.

---

### US2 - People by role and state (Priority: P1)



**Acceptance Scenarios**:

1. **Given** the users page, **When** a role (Customer, Seller, Moderator, Admin) or a state (Active, Locked, Banned) is chosen, **Then** only those people are listed - with the search, and with deleted accounts still left out unless asked (specs/123).

---

### US3 - Categories found and paged (Priority: P2)



**Acceptance Scenarios**:

1. **Given** the categories page, **Then** a search box narrows them by name or slug, and they are shown twelve to a page.

---

### US4 - Find an order reads in words and counts its tabs (Priority: P2)



**Acceptance Scenarios**:

1. **Given** a failed order in Find an order, **Then** its reason reads as the customer's page words it ("ran out of stock", "payment declined"), not the server's text.
2. **Given** the state tabs, **Then** each shows how many orders it holds for the current search.

### Edge Cases

- "Ended" is an active voucher whose end has passed; "Active" one whose end has not (or that has none). Computed in the query against the database's now, so a page and its count agree.
- A person can match more than one role; a role filter lists anybody holding it.
- Categories are one short list the server already returns whole; searching and paging it on the client asks the server nothing new.
- Tab counts are a page of one per tab under keys of their own (specs/130), for the search being shown.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/vouchers/mine` takes `search` (code or name, case-insensitive) and `state` (`Active`, `Ended`, `Disabled`); 400 for another state.
- **FR-002**: `GET /api/users` takes `role` (one of the four) and `state` (`Active`, `Locked`, `Banned`); 400 otherwise.
- **FR-003**: The voucher page has a search box and state tabs with counts; the users page role and state filters; the categories page a search and paging; Find an order worded failures and counts on its tabs.
- **FR-004**: Every filter lives in the page's address, like the existing ones.

## Success Criteria *(mandatory)*

- **SC-001**: Each filter narrows its list as stated (tested against PostgreSQL and in Vitest).

## Assumptions

- No new index: vouchers and users are thousands of rows at most, the filters ride on queries already scoped by owner or paged.
