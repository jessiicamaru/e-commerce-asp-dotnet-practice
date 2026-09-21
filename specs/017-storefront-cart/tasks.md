# Tasks: A Cart and an Address Book

- [X] T001 `client/src/api/cart.ts` and `client/src/api/addresses.ts`: typed calls, plus a readable reason for each line status
- [X] T002 `AddToCart` on the product page, with a sign-in prompt when signed out
- [X] T003 `CartPage`: lines, quantity, remove, estimated total, a reason for each line that cannot be bought
- [X] T004 `AddressesPage`: list, add, edit, delete, make default, with errors shown per field
- [X] T005 Routes `/cart` and `/addresses` behind `RequireAuth`; Cart link in the top bar
- [ ] T006 PR `Closes #37`; CI green; squash-merge

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
