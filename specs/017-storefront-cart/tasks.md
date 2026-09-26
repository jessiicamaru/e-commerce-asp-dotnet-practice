# Tasks: A Cart and an Address Book

> Completed on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `017-storefront-cart`

**Tests**: None automated - the client had no test runner until specs/028. The calls were checked with
curl through the proxy; CI lints and builds.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (a cart that follows the customer), US2 (change it, see the estimate), US3 (address book)

T001-T006 are the list written at the merge, kept as they were.

- [X] T001 `client/src/api/cart.ts` and `client/src/api/addresses.ts`: typed calls, plus a readable reason for each line status
- [X] T002 `AddToCart` on the product page, with a sign-in prompt when signed out
- [X] T003 `CartPage`: lines, quantity, remove, estimated total, a reason for each line that cannot be bought
- [X] T004 `AddressesPage`: list, add, edit, delete, make default, with errors shown per field
- [X] T005 Routes `/cart` and `/addresses` behind `RequireAuth`; Cart link in the top bar

## Recorded after the merge

Added on 2026-09-27 from the diff of #47.

- [X] T007 [US1] Carry the product's path in the sign-in link's `state.from` in `client/src/pages/ProductPage.tsx`, so signing in returns to the product (the sign-in page from specs/015 already honours it)
- [X] T008 [US1] Clamp the quantity input to whole numbers ≥ 1 in `AddToCart`, and show the server's message when an add is refused
- [X] T009 [US2] Show "Prices could not be checked just now. Your items are safe" when `pricesAvailable` is false, and "Remove or fix the items marked above before checking out" when `canCheckOut` is false, in `client/src/pages/CartPage.tsx`
- [X] T010 [P] [US3] `describe()` (an address on one line) and `emptyAddress` in `client/src/api/addresses.ts`
- [X] T011 [P] Styles for the add-to-cart row, the cart lines and the address form in `client/src/index.css`
- [X] T006 PR [#47](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/47) `Closes #37`; CI green; squash-merged as `cf474c7` on 2026-09-22

## Dependencies & Execution Order

T001 first; T002, T003, T004 in parallel after it; T005 once the pages exist. T007-T011 belong to the
files they name.

## What actually happened

Checked through the Vite proxy with a newly registered customer:

```text
add 2 -> 204 · set quantity 3 -> 204 · add -1 -> 400
cart: [Fail Widget, qty 3, 5.00, total 15.00, Available]
logout -> 204, login again -> cart still [(Fail Widget, 3)], estimate 15.0
address "vn" -> saved as VN, default (the first one)
second address, make it default -> 204; list shows only the GB one as default
postal "!" + country "ZZ" -> 400 with errors.PostalCode and errors.Country
edit -> 200 · delete -> 204 · remove line -> 204, cart empty
```

`npm run lint` shows no warnings and `npm run build` succeeds. **Not clicked through in a real
browser**: the pages were type-checked and built, and the calls they make were checked with curl.

## Notes

- **T006 is listed last although its id is lower**: it was the last task at the merge; T007-T011
  describe work already inside that pull request.
- 11 tasks, all done.
