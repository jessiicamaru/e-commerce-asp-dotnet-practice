# Feature Specification: The notices nobody got

**Feature Branch**: `059-missing-notices` | **Created**: 2026-09-24 | **Issue**: #128 (part B of two)

## Why

Specs/042 tells people what happened to them. Three events told nobody:

- **An account locked or banned.** The person learns why only by trying to sign in (specs/049). Once a
  lock has run out, their notifications show nothing about it.
- **A parcel taken as delivered by the 7-day sweep** (specs/040). Its seller is told when the
  **customer** confirms, and not at all when the sweep does. Yet that moment decides when their money
  becomes due.
- **A review hidden by a moderator.** Its author never learns why it disappeared.

## User Scenarios

### US1 - People hear about what staff did to them (P1)

**Acceptance**
1. Locking an account sends `AccountLocked` to its owner, with the end time and the reason. The storefront
   shows the time in the reader's language and time zone.
2. Banning sends `AccountBanned`, with the reason.
3. Hiding a review sends `ReviewHidden` to its author, with the product and the reason, linked to the
   product.

### US2 - A seller hears when the sweep delivers their parcel (P1)

**Acceptance**
1. A seller's parcel taken as delivered by the sweep sends `ParcelAutoDelivered` to that seller. It is
   worded differently from a customer's confirmation, because nobody clicked.
2. The shop's own parcels tell nobody, as for `ParcelReceived`.

### US3 - The storefront has words for all of them (P1)

Each new kind is declared in `notification-kinds.json` (specs/048), with a sentence in Vietnamese and
English. The contract tests on both sides cover them.

## Decision

**Notify on the lock, not only on its end.** A locked person cannot read the notice while locked. After
the lock ends, though, the notice is their record of what happened and why, and the reason is already
shown at sign-in in the meantime. A ban is the same, if it is ever lifted.
