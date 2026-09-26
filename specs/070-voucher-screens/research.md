# Research: Vouchers (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-26

D1 to D3 were first recorded in [plan.md](plan.md#research) and repeated in the PR. Who decided each is not recorded.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - Try a code with the server before keeping it

**Decision**: **Apply** runs `useTryVoucher`, a mutation that asks `Order.quote` with the applied codes plus the new
one. Only if that quote succeeds does the code join the page's state; the summary's own quote is keyed on the choice,
codes included, and follows by itself.

**Rationale**: The refusal is about the code just typed, so it belongs beside the box, and the summary should never be
asked to price a code that breaks it. It reuses the quote - the same code the order is priced by (specs/069) - rather
than a separate "validate" endpoint.

**Alternatives considered**:

- **Add the code to state and let the summary's quote fail.** Rejected: one bad code would replace the whole summary
  with an error, and the customer would have to find and remove it.
- *(reconstructed)* **A dedicated `GET /api/vouchers/{code}/check` endpoint.** Rejected: a server change, and a second definition of
  "usable" that could disagree with checkout; it would also tell a guesser which codes exist.

---

## D2 - One page and one form for both roles

**Decision**: `components/voucher/voucher-page` and `voucher-form` serve the seller and the administrator, with a
`platform` flag. The form offers free delivery and "new customers" only on the platform's page, and "first order in my
shop" only on a shop's. The seller's product picker searches their own products (`useMyProducts`), the administrator's
the whole catalogue (`useProducts`).

**Rationale**: The rules differ in only those three places. The server refuses anything else on its own, and the page
shows its words.

**Alternatives considered**:

- *(reconstructed)* **Two pages.** Rejected: two copies of a long form, to drift.
- *(reconstructed)* **Offer everything and let the server refuse.** Rejected: a seller would be offered free delivery only to be told no.

---

## D3 - The picker chooses products, not variants

**Decision**: Targets from the screen are `Product` ids.

**Rationale**: "20% off this lens" is the case people ask for. Variant targets stay available through the API.

**Alternatives considered**:

- *(reconstructed)* **A variant picker.** Rejected for now: a product-then-variant picker for a case nobody asked for.

---

## Also decided (from the PR)

- The shadcn `checkbox` was added with `shadcn add`, unedited - so the generated-file rule in CLAUDE.md holds for it.
