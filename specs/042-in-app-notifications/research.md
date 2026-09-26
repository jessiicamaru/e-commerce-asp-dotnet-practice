# Phase 0 Research: In-app notifications

> Written on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

D1 to D3 were recorded in the plan when the feature was built and are repeated here with their reasoning in
full. D4 onwards are the decisions the code at the merge embodies but the record did not write down; each is
reconstructed from the code, the pull request and the feature page, and says so where the record is silent.
Two of them (D1's location and D3's polling) were decided with the user on 2026-09-24.

---

## D1 - Kind + data, not text

**Decision**: A notification stores a `Kind` (one of the `NotificationKind` constants) and a `Data` dictionary
of strings, never a sentence. The storefront words it with `describeNotification`, looking up
`notifications:kind.<Kind>` in the reader's current language and filling in the data.

**Rationale**: A sentence stored in the language of the moment would stay in it; the storefront words it from
`Kind` and `Data` in whatever language the reader has now. The shop speaks Vietnamese and English
(specs/021), and a customer who switches language should not find their history in the other one. The same
reasoning made the order freeze its own language (`orders.Language`) - but an order is a record of a
purchase, while a notice is a message to a reader, so here the reader's language wins. Recorded as decision
34 in [docs/project/decisions.md](../../docs/project/decisions.md).

Values are strings on purpose: an amount travels as `"22462000"` formatted with the invariant culture
(`0.##`), and the storefront formats it with the currency for the reader, so a server never decides where the
thousands separator goes.

**Alternatives considered**:

- **Store the sentence in the recipient's language at the time.** Rejected: English for ever for somebody who
  later switches, and the server would need every sentence in every language - the storefront's job.
- **Store a sentence per language.** Rejected: doubles the storage, and a third language would need a backfill
  of every notice ever sent.
- **Kind only, and let the storefront fetch the order to word it.** Rejected: a notice list of twelve would
  cost twelve more requests, and a payout has no order to fetch.

---

## D2 - Same transaction as the change

**Decision**: `INotifier.NotifyAsync` publishes `UserNotificationRequested` through the calling service's
transactional outbox, exactly like `IAuditTrail`. It is called before the one `SaveChangesAsync`, or inside a
repository method's `stage` callback where the change is a guarded statement in its own transaction.

**Rationale**: Settling, moving a parcel, confirming a delivery, cancelling and paying out are guarded
statements in their own transactions; the notification is staged there. A notice about an order that then
failed to save would be a lie in somebody's inbox; a change nobody is told about is the failure this feature
exists to fix. Constitution Principle III makes the ordering non-negotiable, and specs/041 D2 had already
built the `stage` callbacks for the audit entries - the notices join them.

The `stage` runs **only when the guard won** (the settle affected a row, the part moved, the payout claimed
something), so a repeat or a lost race notifies nobody. That is how a redelivered `OrderCompletedEvent` tells
the buyer once: the second settle affects zero rows and its `stage` is never called.

**Alternatives considered**:

- **Publish after `SaveChangesAsync`.** Rejected: the ordering bug this repository has shipped and fixed twice
  (CLAUDE.md, "Transactional Outbox").
- **A separate consumer in Order that listens to its own events and notifies.** Rejected: it would need events
  for moments that have none (a parcel prepared or shipped, a payout), and would notify from a second
  transaction that can disagree with the first.

---

## D3 - Polling

**Decision**: The storefront asks `GET /api/notifications/unread-count` every 30 seconds
(`NOTIFICATION_POLL_MS`), only while the tab is visible (`refetchIntervalInBackground: false`), and loads the
list only when the bell is opened or `/notifications` is visited.

**Rationale**: 30 s on the unread count only; the list loads when the bell opens. Decided with the user. The
notices at this merge are about orders and money moving over hours and days; half a minute of delay costs
nothing, and a count is one indexed `count(*)` (see [data-model.md](./data-model.md), `IX_notifications_unread`).
A background tab asking twice a minute all afternoon is load for nobody.

**Alternatives considered**:

- **WebSocket or server-sent events.** Rejected with the user: a long-lived connection through YARP, a hub in
  Activity, reconnect logic and a new failure mode, for a delay nobody would notice.
- **Poll the list.** Rejected: twelve rows every 30 s for every open tab, to answer a question - "anything
  new?" - that one number answers.

---

## D4 - The inbox lives in Activity, beside the audit log

**Decision**: The `notifications` table, its consumer and its endpoints are in the Activity service
(specs/041), not in a new service and not in each publishing service.

**Rationale**: Decided with the user. specs/041 D1 created Activity as the sink for "things that happened",
anticipating #87 by name. A notice and an audit entry travel the same path - published through the source's
outbox, stored once by a consumer keyed on the publisher's id - so they share the service, its database, its
consumer outbox and its gateway cluster. One inbox per person also means one bell, whichever service spoke.

**Alternatives considered**:

- **A Notifications service of its own.** Rejected: a fifth database and a ninth service for one table, and
  the same machinery Activity already has.
- **Each service keeps the notices it sends.** Rejected: the bell would ask every service, and a person's
  inbox would be split across databases (constitution Principle I puts one fact in one place).

---

## D5 - Idempotent by the publisher's id

**Decision**: The publisher mints `NotificationId` (`Guid.CreateVersion7()` in `Notifier`), it becomes the
row's primary key (`ValueGeneratedNever()`), and Activity inserts with
`INSERT ... ON CONFLICT ("Id") DO NOTHING`. Activity's consumer endpoints also run MassTransit's EF inbox
(`UseEntityFrameworkOutbox<ActivityDbContext>`), as they did for the audit log.

**Rationale**: FR-004, and constitution Principle III: idempotency enforced by a database constraint, not by
configuration alone. The primary key is the constraint. `ON CONFLICT DO NOTHING` rather than catch-and-ignore
because a failed insert leaves the row tracked and the next save retries it (the CLAUDE.md gotcha about
`SaveChangesAsync` not untracking), and because four concurrent deliveries must end with one row without any
of them failing - `A_notification_lands_in_its_recipient_s_inbox_once` sends four at once.

**Alternatives considered**:

- **The inbox alone.** Rejected: the guarantee would rest on configuration no test catches regressing
  (the same reasoning as specs/001 D3).
- **Activity mints the id.** Rejected: a redelivery would be a new id and a second notice.

---

## D6 - Settling joins the consumer's transaction

**Decision**: `IOrderRepository.TrySettleAsync` takes an optional `stage`. With one, it checks
`Database.CurrentTransaction`: when a transaction is already open - the consumer outbox's - it runs the guarded
`UPDATE ... WHERE Status = 'Submitted'`, calls `stage` if a row changed, and saves, **inside that transaction**;
otherwise it opens its own through `CreateExecutionStrategy().ExecuteAsync`.

**Rationale**: Found only in the running stack. The saga's outcome reaches Order through
`OrderCompletedConsumer` / `OrderFailedConsumer`, and MassTransit's consumer outbox has already opened a
transaction on the context by then. The first version of `TrySettleAsync` opened a second one, which throws
`InvalidOperationException: The connection is already in a transaction` - and every order stayed `Submitted`,
while every unit test passed, because they sent the command outside a consumer. Joining keeps the settlement,
the audit entry, the notices and MassTransit's inbox row in one commit. Clearing the change tracker there, as
the own-transaction branch does, would drop the inbox row MassTransit staged.
`Settling_inside_a_consumer_transaction_joins_it` opens the transaction the way MassTransit does; with the
join branch disabled it fails with the production error (the pull request's mutation check).

**Alternatives considered**:

- **Always open a new transaction.** Rejected: it is the bug.
- **Stage the notices outside `TrySettleAsync`, after it returns.** Rejected: the settle's guarded update runs
  directly against the database, so a notice staged afterwards could be saved when the settle had lost - or,
  in the consumer path, be the only thing that decides whether anything is told.

---

## D7 - The reader is in the `WHERE`, and not-yours is "not found"

**Decision**: No endpoint takes a user id. Every query and update in `NotificationRepository` has
`RecipientId = <caller>` in its `WHERE`, the caller coming from `ICurrentUser`. Marking someone else's notice,
or a notice that does not exist, throws the same `NotFoundException("Notification not found.")`; marking one's
own already-read notice is a quiet success.

**Rationale**: Constitution Principle IV and the project's "not yours is 404, never 403" rule (specs/027,
specs/034): a 403 would confirm the id is real and belongs to somebody. Putting the recipient in the statement
means another person's row is never touched, not merely never returned. `TryMarkReadAsync` returns
`true` / `false` / `null` (marked / already read / not theirs) so the handler can tell a repeat from a
stranger without a second rule. An administrator has no special reach: an inbox is personal, and Bruno's
"marking another person notice is 404" uses the admin token to prove it.

**Alternatives considered**:

- **`GET /api/notifications?userId=`, checked against the token.** Rejected: the `UserId`-in-the-request shape
  the constitution forbids.
- **Load the row, then compare its recipient in code.** Rejected: two statements where one suffices, and a
  check in code is one refactor from being lost - the owner filter is exactly what the pull request's second
  mutation check removed.

---

## D8 - Who is told what, in one place

**Decision**: Order decides recipients and data in one static class, `OrderNotices`, from an
`OrderNoticeFacts` record (buyer, total, currency, and each parcel with its seller and shop name) that
`GetNoticeFactsAsync` reads inside the transaction making the change. Every path - the saga's settle, a
seller's or the staff's ship, the customer's confirmation, a cancellation by either side, a payout - calls it.

**Rationale**: Staff ship the shop's parcel through one handler and sellers ship theirs through another; a
customer and an administrator cancel through two. Words chosen in each handler would drift. Reading the facts
inside the transaction means they describe the order as the change left it. `GetNoticeFactsAsync` first calls
`EnsureShipmentsAsync`, so an order written by an older image without parcel rows still has parts to name
(specs/035).

Sellers are `Distinct()`, so a seller with two lines is told once, and parcels with a null seller - the shop's
own - produce no seller notice, because the shop has nobody to tell.

**Alternatives considered**:

- **Each handler builds its own notice.** Rejected for the drift above.
- **Carry the facts on the command.** Rejected: the saga's `OrderCompletedEvent` carries only an order id, and
  the facts would be read before the transaction rather than in it.

---

## D9 - An unknown kind is shown, generically

**Decision**: `describeNotification` asks i18next for `kind.<Kind>` with an empty default; an empty answer
becomes "You have a new update." (`generic`).

**Rationale**: A newer service may send a kind before the storefront has its words - later features added
kinds in Identity and Catalog. A notice that vanished because the client was a release behind would be a lie
by omission; a generic line still points at the thing (its link is used either way).

**Alternatives considered**:

- **Hide it.** Rejected for the reason above.
- **Show the kind code (`ShopApproved`).** Rejected: a code in a sentence is what a customer should never read.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| The data keys a kind carries are agreed between two codebases by convention | A server sending `amount` where the storefront reads `total` shows a sentence with a hole | Not mitigated at this merge. specs/048 (#119) later declared every kind's keys in `notification-kinds.json` and tested both sides against it, after five kinds reached people with placeholders |
| No retention | `notifications` grows without bound | Recorded as a known limit in the feature page; out of scope |
| Activity down | Notices are not visible | They wait in the publishers' outboxes and the broker; nothing is lost |
