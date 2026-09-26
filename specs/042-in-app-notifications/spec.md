# Feature Specification: In-app notifications

> Completed on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature branch**: `042-in-app-notifications` | **Issue**: #87 | **Created**: 2026-09-24 | **Status**: Draft

**Status at completion**: Merged by PR #94 (2026-09-23 20:26 UTC, which is 2026-09-24 in the project's local
time; the spec's creation date is in local time too).

**Input**: Issue #87, "feat(shared): in-app notifications": a per-user inbox in the Activity service, fed by a
`UserNotificationRequested` contract that services publish through their outboxes; text rendered by the
storefront from `Kind` + `Data`; notices for what already exists (order paid, parcel shipped, order cancelled for
the customer; new sale, parcel received, payout recorded for the seller); a bell and a notifications page.

## What is wrong

Nobody is told anything. A customer learns their parcel shipped only by opening the order; a seller learns
they sold something only by opening their sales.

Before this feature the system already knew every one of these moments - the saga settles an order, a parcel
moves through a guarded update, an administrator records a payout - and each is audited since specs/041. None
of it reached the person it concerned. The issue adds a second reason it matters: moderation decisions about a
seller's shop and products were about to arrive (#89-#91), and a seller would learn the answer the same way,
by going to look.

## Decided with the user (2026-09-24)

- Notifications live in the **Activity** service with the audit log (specs/041).
- The storefront **polls every 30 seconds**; no WebSocket.

## User Scenarios & Testing *(mandatory)*

### US1 - People hear about what concerns them (P1)

| Who | When |
| :-- | :-- |
| Customer | their order is paid; it failed; a parcel is shipped; it is cancelled |
| Seller | a new sale; a sale is cancelled; their parcel was received; a payout was recorded for them |

1. Each produces exactly one notification for the right person, even when the message is redelivered.
2. Nobody else sees it.

**Why this priority**: This is the feature. A bell with nothing behind it is decoration; the value is that the
moment an order moves, the buyer and each seller on it hold a record of it without having to look.

**Independent Test**: Place an order with goods from two sellers, let the saga pay it, and read each person's
inbox: the buyer holds one `OrderPaid`, each seller one `NewSale`, and delivering the saga's outcome a second
time adds nothing.

**Acceptance Scenarios**:

1. **Given** an order with goods from two sellers, **When** the saga reports it paid, **Then** the buyer is
   told once that it was paid (with its total and currency) and each of the two sellers is told once of a new
   sale.
2. **Given** that order was already settled as paid, **When** the same outcome is delivered again, **Then**
   nobody is told a second time.
3. **Given** an order the saga reports as failed, **When** it settles, **Then** the buyer is told it failed
   and no seller is told anything.
4. **Given** a seller's parcel being prepared, **When** the seller ships it with a tracking reference, **Then**
   the buyer is told a parcel shipped, with the tracking reference and the seller's shop name.
5. **Given** a shipped seller's parcel, **When** the customer confirms it arrived, **Then** that parcel's
   seller is told it was received.
6. **Given** a paid order with a seller's goods and the shop's own goods, **When** the customer cancels it,
   **Then** the buyer is told it was cancelled and by whom, and the seller is told the sale was cancelled.
7. **Given** a seller with delivered parcels due, **When** an administrator records a payout, **Then** the
   seller is told how much was recorded and in which currency.
8. **Given** any of the above, **When** a different signed-in person reads their inbox, **Then** it does not
   contain the notice.

---

### US2 - The bell (P1)

1. The header shows a bell with the unread count, refreshed every 30 seconds.
2. Opening it lists the latest notifications, newest first, **in the reader's language**; choosing one marks
   it read and goes where it points (the order, the sale, the payouts page).
3. "Mark all read" clears the count. A notifications page lists all of them, a page at a time.

**Why this priority**: Equal first with US1, because a notice nobody can see has not told anybody. The two
ship together: US1 without US2 fills inboxes nobody opens, US2 without US1 is an empty bell.

**Independent Test**: With two unread notices in an inbox, the bell shows 2; opening it lists them newest
first; choosing one opens its order and the bell shows 1; "Mark all read" makes it 0.

**Acceptance Scenarios**:

1. **Given** a signed-in person with two unread notices, **When** any page loads, **Then** the bell shows 2
   and asks again every 30 seconds while the tab is visible.
2. **Given** the bell is closed, **When** the page is idle, **Then** only the count is asked for; the list is
   loaded when the bell is opened.
3. **Given** the bell is open, **When** the reader chooses an unread notice, **Then** it is marked read and the
   storefront goes to its link.
4. **Given** unread notices, **When** the reader chooses "Mark all read", **Then** the count becomes 0.
5. **Given** more notices than fit on a page, **When** the reader opens `/notifications`, **Then** they are
   listed newest first, twelve at a time, with an "Unread" tab showing only those not yet read.
6. **Given** a visitor who is not signed in, **When** a page loads, **Then** no bell is shown and nothing is
   asked for.

---

### US3 - The words follow the reader (P2)

A notice is read in the language the reader has chosen **when they read it**, not the language of the moment
it was sent.

**Why this priority**: The shop speaks Vietnamese and English (specs/021). It is second because it is a
property of how US1's notices are stored and worded rather than a separate capability, but it cannot be added
afterwards: notices stored as sentences would stay in whatever language they were written in.

**Independent Test**: Read the same stored notice with the storefront in English, then in Vietnamese; both
read as sentences in their language, and a notice whose kind the storefront does not know still shows.

**Acceptance Scenarios**:

1. **Given** one stored `OrderCancelled` notice, **When** it is read in English and then in Vietnamese,
   **Then** it reads "cancelled by you" in one and "bởi ..." in the other - the stored code (`Customer`,
   `Staff`) is never shown.
2. **Given** a `ParcelShipped` notice for the shop's own parcel (no shop name in its data), **When** it is
   read, **Then** it names "the shop" in the reader's language.
3. **Given** a notice of a kind this storefront has no words for, **When** it is read, **Then** it shows as
   "You have a new update." rather than being hidden.

---

### Edge Cases

- **Redelivery.** The broker delivers at least once. A redelivered `UserNotificationRequested` must not make a
  second row; a redelivered saga outcome must not publish a second notice.
- **The shop's own goods.** The shop (a null seller) has nobody to tell: no `NewSale`, `SaleCancelled` or
  `ParcelReceived` goes out for its parcel. The buyer is still told when the shop's parcel ships.
- **One seller, several items.** A seller with more than one line on an order is told once, not once per line.
- **Someone else's notice id.** Marking it read is "not found" - the same answer as an id that does not exist,
  so an id is never confirmed to belong to somebody.
- **Marking twice.** Marking an already-read notice read again is not an error.
- **A kind the storefront does not know.** A newer service may send one before the storefront learns it; it is
  shown generically, never dropped.
- **An order from before currencies were frozen.** Its `OrderPaid` carries an empty currency; the storefront
  then shows no amount rather than a wrong one.
- **Settling inside a consumer.** The saga's outcome arrives through a consumer that already holds a database
  transaction; the notice must commit with that settlement, not in a second transaction.
- **A parcel delivered by the 7-day sweep.** Nobody confirmed it, so no `ParcelReceived` is sent at this merge
  (a separate kind, `ParcelAutoDelivered`, arrived with specs/059).
- **Activity down.** Notices wait in the publishers' outboxes and the broker; nothing is lost, nothing is
  visible until it returns.

## Requirements *(mandatory)*

- **FR-001** A notification is written in the same transaction as the change it announces.
  *Completed on 2026-09-27, to be exact about "written":* what is written in that transaction is the outbox
  message carrying the notice. The inbox row is written by the Activity service when the message arrives, a
  moment later; a change that rolls back therefore leaves no notice, and a change that commits always
  produces one.
- **FR-002** Stored as a kind and data, never as a sentence - the storefront words it in the reader's language.
- **FR-003** A user reads and marks only their own; someone else's is "not found".
- **FR-004** At most one per notification id.
- **FR-005** Each event in US1 MUST notify exactly the people named there: the buyer for paid, failed, shipped
  and cancelled; each distinct seller on the order for a new sale and a cancelled sale; the parcel's seller for
  a received parcel; the paid seller for a payout.
- **FR-006** A settlement or step that does not happen (a repeat, a lost race, a refused move) MUST notify
  nobody.
- **FR-007** Each notice MUST carry a link to where it is about - the order for a customer, the sale for a
  seller, the payouts page for a payout.
- **FR-008** The reader MUST be able to see how many notices are unread, list them newest first a page at a
  time (all, or only unread), mark one read, and mark all read.
- **FR-009** The storefront MUST refresh the unread count at most every 30 seconds and not while the tab is in
  the background; it MUST load the list only when the reader asks for it.
- **FR-010** An inbox MUST be reachable only when signed in; the identity of the reader MUST come from the
  access token, never from the request.

### Key Entities

- **Notification**: One thing one person was told. Who it is for, what kind of thing happened, the facts the
  words need (an order reference, an amount, a tracking reference, a shop name), where it points, when it
  happened, and when - if ever - its recipient read it. Its identity is chosen by the service that sent it, so
  a redelivery is the same notification.
- **Notification kind**: The closed list of things that can be said at this merge - `OrderPaid`,
  `OrderFailed`, `ParcelShipped`, `OrderCancelled`, `NewSale`, `SaleCancelled`, `ParcelReceived`,
  `PayoutRecorded` - each with words in both languages in the storefront.

## Success Criteria *(mandatory)*

- **SC-001** Every event in US1 yields exactly one notification to exactly the right people.
- **SC-002** `verify-saga.sh` and Bruno pass.
- **SC-003** A notice id belonging to someone else and an id that does not exist produce the same refusal,
  word for word.
- **SC-004** After "mark all read" the unread count is 0 at the next ask; after marking one read it is one
  lower.
- **SC-005** A notice reaches the bell within one polling interval (30 seconds) of its message being stored.
- **SC-006** Delivering the same notice four times at once leaves exactly one row in the recipient's inbox.

## Assumptions

- The Activity service from specs/041 exists, with its database, its consumer outbox and its gateway cluster;
  this feature adds a table and endpoints to it rather than a new service.
- Order is the only service with something to say at this merge. Identity and Catalog gained notices in later
  features (#89-#91, specs/043-045) through the same interface.
- Up to 30 seconds between a notice being stored and the bell showing it is acceptable - decided with the user.
- The storefront knows every kind a server sends at the time of release; a kind it does not know is a newer
  server ahead of it, and is shown generically.
- A seller and the buyer are different people. The case of a seller buying their own goods is not treated
  specially (not recorded as considered).

## Out of scope

- Email, push, SMS; preferences; notifications for moderation (they arrive with #89-#91).
- Real-time delivery (WebSocket, server-sent events).
- Retention, archiving or deleting notices.
- A notice for a parcel the delivery sweep takes as delivered (later: `ParcelAutoDelivered`, specs/059).
