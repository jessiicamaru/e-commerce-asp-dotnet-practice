# Feature Specification: In-app notifications

**Feature branch**: `042-in-app-notifications` | **Issue**: #87 | **Created**: 2026-09-24 | **Status**: Draft

## What is wrong

Nobody is told anything. A customer learns their parcel shipped only by opening the order; a seller learns
they sold something only by opening their sales.

## Decided with the user (2026-09-24)

- Notifications live in the **Activity** service with the audit log (specs/041).
- The storefront **polls every 30 seconds**; no WebSocket.

## User Scenarios

### US1 - People hear about what concerns them (P1)

| Who | When |
| :-- | :-- |
| Customer | their order is paid; it failed; a parcel is shipped; it is cancelled |
| Seller | a new sale; a sale is cancelled; their parcel was received; a payout was recorded for them |

1. Each produces exactly one notification for the right person, even when the message is redelivered.
2. Nobody else sees it.

### US2 - The bell (P1)

1. The header shows a bell with the unread count, refreshed every 30 seconds.
2. Opening it lists the latest notifications, newest first, **in the reader's language**; choosing one marks
   it read and goes where it points (the order, the sale, the payouts page).
3. "Mark all read" clears the count. A notifications page lists all of them, a page at a time.

## Requirements

- **FR-001** A notification is written in the same transaction as the change it announces.
- **FR-002** Stored as a kind and data, never as a sentence - the storefront words it in the reader's language.
- **FR-003** A user reads and marks only their own; someone else's is "not found".
- **FR-004** At most one per notification id.

## Out of scope

- Email, push, SMS; preferences; notifications for moderation (they arrive with #89-#91).

## Success Criteria

- **SC-001** Every event in US1 yields exactly one notification to exactly the right people.
- **SC-002** `verify-saga.sh` and Bruno pass.
