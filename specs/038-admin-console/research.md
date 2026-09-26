# Research: An administrator's console

> Written on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md. D1-D3 were recorded in plan.md's Research
> section before the code; they are restated here with rationale and alternatives, and plan.md keeps its
> copy. D4-D6 are decisions the code and the pull requests show.

**Feature**: [spec.md](spec.md)

## D1 - The shop's parcel is read from `shipments[]`

**Decision**: the shop's parcel is the part with `isShop`. An order an older image wrote has no parts;
then the order's own status is the shop's (specs/035 D3).

**Rationale**: specs/035 made the shop's goods a part like any seller's, and specs/036 marked it with
`isShop`. The client function `shopParcelOf` (client/src/pages/admin-order/shop-parcel.ts) is the one
place that decides, and returns `null` when the order holds none of the shop's goods - which is what
draws "each seller ships their own" with no action. For an order with no parts it treats the whole
order as the shop's parcel, which is exactly what the server's staff endpoints move.

**Alternatives considered**: taking any parcel as the shop's - one of the seven mutations in #82, and a
test turned red.

## D2 - Staff see the whole order

**Decision**: including the customer's address and every seller's parcel.

**Rationale**: staff are the shop, and the fulfilment queue already shows them every order's total. They
cannot ship what they cannot see, and seeing a seller's parcel on the same order is how they know it is
somebody else's job.

**Alternatives considered**: none recorded.

## D3 - Pay is confirmed in an AlertDialog

**Decision**: the dialog names the shop and the amount the list showed. The server pays what is due
*now*, which can be more if another parcel shipped meanwhile; the toast shows what was actually recorded.

**Rationale**: `POST /api/orders/payouts` takes no amount (specs/037) - there is nothing in the request
to disagree with the parts - so the confirmation is for the person, and the toast reports the truth.

**Alternatives considered**: sending the amount shown - rejected by specs/037's design, and "an amount
sent" was one of #82's mutations, caught by a test.

## D4 - A new Admin-only read, not a widened owner read

**Decision**: `GET /api/orders/fulfilment/{id}`, `[Authorize(Roles = "Admin")]`, served by
`GetOrderForStaffQuery` with no owner in the query.

**Rationale**: the owner-scoped `GET /api/orders/{id}` must stay owner-scoped for everybody else; adding
"or an administrator" to it would put the exception inside the rule. A separate route makes the role the
whole permission, visibly - and the handler's remarks say never to reuse it behind any route that is not
Admin-only. Bruno checks that a customer and a seller both get 403.

**Alternatives considered**: reading through the owner-scoped route - one of #82's mutations, caught.

## D5 - One parcel-actions component for a seller's parcel and the shop's

**Decision**: `SaleActions` became `components/order/parcel-actions`, taking a status, a tracking
reference and the two mutations.

**Rationale**: the steps and the rule (next step only) are the same for both; two components would be
two places to get the rule wrong.

**Alternatives considered**: none recorded.

## D6 - A confirmed dialog must close (the follow-up, #83)

**Decision**: `AlertDialogAction` in `client/src/components/ui/alert-dialog.tsx` is a `Close`, like
`AlertDialogCancel`. The payouts page first controlled its dialog itself (#82); #83 fixed the component.

**Rationale**: in this base-ui shadcn the generated `AlertDialogAction` was a plain `Button` that did not
close the dialog. After "Record" the dialog stayed over the page and hid the server's refusal - found by
the payouts test in #82. The same latent defect was in withdrawing a product and deleting an address. It
makes `alert-dialog.tsx` the fourth edited file under `ui/`, which `shadcn add --overwrite` would undo; a
test in `address-card` (`closes the dialog once confirmed`) fails if it is.

**Alternatives considered**: controlling each dialog by hand in every page - what #82 did for payouts,
replaced by the one fix in #83.
