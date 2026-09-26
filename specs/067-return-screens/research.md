# Research: Returning a delivered parcel (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-25

D1 to D3 were first recorded in [plan.md](plan.md#research); they are repeated here with their alternatives in the
standard shape, and D4 is added from the pull request. Who decided each is not recorded.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - How the client learns the 7 days

**Decision**: A constant, `RETURN_WINDOW_DAYS = 7` in `client/src/constants/order`, used for drawing only. The pure
functions in `client/src/utils/order/returns.ts` copy each server guard, including the edge: exactly 7 days is closed
(`now < from + 7 days`, the server's `DeliveredAt <= now - window` refusal seen from the other side).

**Rationale**: The PR is client-only, and the server's `ShipmentResponse` is built by a static mapping that has no
access to options. If the two ever disagree, the server refuses with a 409 that names the window and the page shows
it - the same bargain as `customerCanCancel`.

**Alternatives considered**:

- **A `returnableUntil` field on `ShipmentResponse`.** Rejected: a server change, to a mapping with no options, in an
  otherwise client-only PR.
- *(reconstructed)* **Offer every step and let the server refuse.** Rejected: a page full of buttons that answer "too late" is worse
  than one that draws what can be done.

---

## D2 - One decision component for the seller and staff

**Decision**: `components/order/return-decision` draws the reason, the state and the accept / refuse / received steps
for both. The caller hands in whose mutations they are; a `final` flag words a refusal as "Reject for good".

**Rationale**: Same steps, same confirmation; only the endpoints differ. The same idea as `ParcelActions`
(specs/038).

**Alternatives considered**:

- *(reconstructed)* **A component per role.** Rejected: two copies of the same confirmation and wording, to drift apart.

---

## D3 - The queue shows no amounts

**Decision**: `/admin/returns` lists state, dates and the order link, never `refundAmount`.

**Rationale**: `ReturnResponse` carries no currency, and a number without its currency is the thing specs/022 forbids.
The amount is on the order page, in the order's currency.

**Alternatives considered**:

- *(reconstructed)* **Add the currency to `ReturnResponse`.** Rejected here as a server change; not needed for a queue whose job is to
  say what is waiting.

---

## D4 - One shared text dialog for every step that needs words

**Decision**: `components/shared/text-prompt` asks for the reason of a request, the reason of a refusal and the
tracking reference. It will not send empty text, closes only once the server accepts, and shows a refusal inside
the dialog. Accepting and marking received, which need no words but cannot be undone, are confirmed in a dialog
first.

**Rationale**: Three prompts with the same three rules. Keeping the dialog open on a 409 is what lets the server's
words be read (spec US1 acceptance 5).

**Alternatives considered**:

- *(reconstructed)* **Inline inputs on the page.** Rejected: an empty reason and a lost refusal message are exactly what the dialog
  prevents.

---

## Also decided (from the PR)

- **No return badge on the sales list.** The seller learns of a return from the `ReturnRequested` notice, which links
  to the sale.
- **The delivery-confirmation dialog changed its words**: it used to say confirming "releases the seller's payment";
  since specs/066 it starts the 7-day return window, and now says so in both languages.
