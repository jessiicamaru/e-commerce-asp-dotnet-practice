# Phase 0 Research: The notices nobody got

> Written on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-24

Five decisions. D1 was recorded in the pull request as "decided on the user's behalf"; the rest are reconstructed
from the code and the pull request.

---

## D1 - Notify on the lock itself, not only when it ends

**Decision**: Send `AccountLocked` when the lock is set (and `AccountBanned` when the ban is).

**Rationale**: The person cannot read the notice while locked, and the reason is shown at sign-in meanwhile
(specs/049). Afterwards, the notice is their record of what happened and why - the one place it survives once the
lock has run out. A ban is the same, if it is ever lifted.

**Alternatives considered**:

- **Notify when the lock ends.** Rejected: nothing runs at the moment a lock expires (it is a timestamp compared
  at sign-in), so this would need a sweeper for a notice that says less than one sent at the time.
- **No notice; rely on the sign-in refusal.** Rejected: that is the gap #128 describes.

---

## D2 - `until` is stored as ISO 8601 UTC and worded by the storefront

**Decision**: The lock notice carries `until` as `DateTime.SpecifyKind(user.LockedUntil, Utc).ToString("o")`.
`describeNotification(t, n, language)` gains a third parameter and formats it with
`new Date(until).toLocaleString(language)`; the bell and the notifications page pass `i18n.language`.

**Rationale**: specs/042 stores a kind and data, never a sentence, so a notice reads in whichever language its
reader chooses now. A moment is data of the same kind: the server cannot know the reader's time zone, the
browser can.

**Alternatives considered**:

- **Store a formatted date.** Rejected: it would be in one language and one time zone, the defect specs/042
  exists to avoid.
- **Store the number of days.** Rejected: "locked for 2 days" read three days later tells the person nothing
  about when it ends.

---

## D3 - The sweep's notice is staged inside the sweep's transaction

**Decision**: In `AutoConfirmDeliveriesCommandHandler`, the `stage` callback that already records the audit entry
and announces `ParcelDeliveredEvent` now also reads each delivered parcel's order facts
(`GetDeliveredParcelsAsync`, `OrderNotices.WithFactsAsync`) and calls `OrderNotices.AutoDeliveredAsync` for its
seller.

**Rationale**: The sweep's guarded statement decides which parcels it took; only inside its transaction are those
exactly the parcels being announced. A notice staged elsewhere could name a parcel another instance took, or be
sent for a sweep that rolled back.

**Alternatives considered**:

- **Consume `ParcelDeliveredEvent` in Order and notify from the consumer.** Rejected: the event does not say
  whether the customer or the sweep delivered it, and a consumer in the publishing service would be a second
  transaction for no gain.
- **Reuse `ParcelReceived`.** Rejected: see D4.

---

## D4 - A separate kind, worded differently, and nothing for the shop's own parcel

**Decision**: `ParcelAutoDelivered` is its own kind, "Your parcel of order {{order}} was taken as delivered a
week after it shipped." `AutoDeliveredAsync` finds the parcel's `SellerId`; a null seller (the shop's own) tells
nobody, as `ReceivedAsync` already does.

**Rationale**: `ParcelReceived` says the customer confirmed. Saying so when nobody clicked would be false, and a
seller reading it would think the customer was satisfied. The shop has no seller inbox to tell.

**Alternatives considered**: `ParcelReceived` with a `by` key - rejected: the storefront would word one kind two
ways, and every existing notice of that kind would need the key it never carried.

---

## D5 - The hidden review's notice rides the guarded hide

**Decision**: The hide handler reads the product first, then passes a `stage` callback to
`ReviewRepository.TryHideAsync` (specs/057, #140) that records the audit entry **and** notifies the author with
`product`, `reason` and the link `/products/{productId}`.

**Rationale**: One guarded statement decides between two moderators hiding at once. Staging the notice in its
callback means only the winner tells the author; the loser gets 409 and sends nothing.

**Alternatives considered**: notify after `TryHideAsync` returns 1 - rejected: that is a second save after the
decision, the ordering Principle III forbids.
