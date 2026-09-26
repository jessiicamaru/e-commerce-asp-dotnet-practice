# Research: Notification wording

> Written on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

D1-D4 were first written in [plan.md](plan.md) and are restated here in the standard form. D5 is recorded
in the pull request's "Decided on the user's behalf".

---

## D1 - One declaration, in JSON, beside `NotificationKind`

**Decision**: `Ecommerce.Shared/Notifications/notification-kinds.json` maps each kind to its `required`
and `optional` data keys. It is embedded in `Ecommerce.Shared` (logical name
`Ecommerce.Shared.Notifications.notification-kinds.json`) and read by the server tests through
`NotificationContract`; the client test imports the same file by relative path
(`../../../../server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`).

**Rationale**: the defect was two lists that drifted - what services send and what the storefront reads.
One file both sides are tested against makes the drift a red test on whichever side changed. JSON is the
one format both a C# test and a TypeScript test read without a build step.

**Alternatives considered**:

- **A list on each side.** Rejected: this is what drifted.
- **Code generation.** Rejected: a build step for sixteen entries.
- **Parsing the C# from the client test.** Rejected: fragile.

---

## D2 - Checked by the tests, never at run time

**Decision**: `NotificationContract.Problems(kind, data)` is called only from tests. `Notifier` does not
validate.

**Rationale**: `Notifier` could validate the data and throw. But a wording mismatch would then roll back a
payout or a moderation decision, which is a far worse defect than a clumsy sentence. The existing tests
already publish every kind through the real `Notifier`. Asserting on what they capture checks the call
sites that actually run, not a copy of them.

**Alternatives considered**:

- **Validate in `Notifier` and throw.** Rejected for the reason above.
- **Validate in `Notifier` and log.** Not recorded as considered.

---

## D3 - A hole means the generic sentence

**Decision**: `describeNotification` first reads the sentence's raw form (`t(key, { skipInterpolation:
true })`), lists its `{{placeholders}}` (ignoring i18next's own `count`), and when any would be empty
returns `t('generic')` - "You have a new update."

**Rationale**: it is generic over kinds, so a future kind cannot reintroduce the defect silently. A vaguer
sentence beats one with a hole in it, and a notice stored by an older service with fewer keys still reads.

**Alternatives considered**: a per-kind list of required values in the client. Not recorded as
considered; it would be a third list to drift, which D1 exists to avoid.

---

## D4 - Plural rating

**Decision**: English `NewReview_one` / `NewReview_other`, with `count` set to the rating when it parses
as a finite number. Vietnamese has no plural and keeps the one string.

**Rationale**: "1 stars" is the kind of sentence the feature exists to remove. i18next's plural suffixes
need no code beyond passing `count`.

**Alternatives considered**: not recorded.

---

## D5 - "The shop" only for `ParcelShipped`

**Decision**: a missing `shop` value reads "the shop" only on `ParcelShipped`; on any other kind a missing
`shop` is a hole and D3 applies.

**Rationale**: on a parcel, no seller means the shop's own goods, so "the shop" is true. Before this, the
default (`d.shop ?? t('theShop')`) applied to every kind, so a `ShopApproved` notice with no name would
have named the applicant's shop "the shop" - wrong rather than vague. The pull request records this as decided on the user's behalf.

**Alternatives considered**: keeping the default for every kind (the code before) - rejected for the
reason above.
