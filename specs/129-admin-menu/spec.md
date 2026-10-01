# Feature Specification: The admin console's menu is grouped and shows what is waiting

**Feature Branch**: `feat/246-admin-menu` | **Created**: 2026-10-01 | **Issue**: #246

**Status**: Draft

**Input**: Issue #246 - "the admin console's menu is grouped and shows what is waiting" - found in the screen review of 2026-10-01.

## Why

The administrator's sidebar was 19 links in one column - orders, money, catalogue, moderation, people, messages - with nothing to say where work was waiting: 14 products could be pending review and the menu looked the same as with none. Opening an order highlighted nothing, and its breadcrumb said "Orders to ship" even when the order was found through search or the returns queue.

## User Scenarios & Testing *(mandatory)*

### US1 - The menu is grouped (Priority: P2)



**Acceptance Scenarios**:

1. **Given** an administrator, **Then** the links sit under headings: Orders, Money, Catalogue, Moderation, Messages, System - with the overview first.
2. **Given** a moderator, **Then** only the groups they can use appear (Moderation), as before only the links.

---

### US2 - The menu shows what is waiting (Priority: P2)



**Acceptance Scenarios**:

1. **Given** parcels waiting to ship, returns escalated to staff, products and shop applications waiting for review and open reports, **Then** each has a count beside its link; with none, no badge.
2. **Given** a moderator, **Then** only the counts of their own queues are asked for - no request that would be refused.

---

### US3 - An order says where it was opened from (Priority: P3)



**Acceptance Scenarios**:

1. **Given** an order opened from Find an order, the returns queue or the fulfilment queue, **Then** that link is highlighted and the breadcrumb leads back to it, its filters included; opened directly, it falls back to Orders to ship.

### Edge Cases

- The counts are read once with the layout and every minute after (and when the tab regains focus) - not live; a queue's own page is always current.
- A count reads the queue's `totalCount` with a page of one, under its own query key: sharing the list's key (which carries no page size) would put a one-row page on the list's screen.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The admin sidebar groups its links under headings; headings with no link the person may use are not drawn.
- **FR-002**: `useStaffWaiting({ isAdmin })` reads five totals - fulfilment Waiting, returns Escalated (Admin); products Pending, shop applications Pending, open reports (staff) - and the sidebar badges them.
- **FR-003**: Links to an order pass `state.from`; the order page's breadcrumb returns there, and the layout highlights the link that page belongs to.

## Success Criteria *(mandatory)*

- **SC-001**: An administrator sees the five counts and six groups; a moderator only theirs (tested).
- **SC-002**: An order opened from search highlights Find an order (tested).

## Assumptions

- No new endpoint: each queue's list already returns `totalCount`.
