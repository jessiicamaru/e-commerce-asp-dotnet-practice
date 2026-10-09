---
description: "Task list for A cart before signing in"
---

# Tasks: A cart before signing in

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 Extract `CartPricing` from `GetMyCartQueryHandler`; the stored cart reads through it unchanged
- [x] T002 `POST /api/cart/price` (anonymous, stores nothing) and `POST /api/cart/merge` (larger quantity, one
  transaction); validators (50 lines, 1-999)
- [x] T003 `GuestCartTests` on PostgreSQL: pricing equal to the stored cart's and writing nothing, the merge rule, a
  repeated merge, refusals; a mutation

## Client

- [x] T004 The browser store (`utils/cart/guest-cart`), service methods, hooks reading either cart
- [x] T005 Add to cart signed out; the header's count; `/cart` open signed out, checkout to sign-in and back
- [x] T006 The merge wherever a sign-in ends
- [x] T007 Client tests; locales

## Contract, docs

- [x] T008 Bruno for both endpoints and their refusals; `docs/reference` regenerated
- [x] T009 On the stack: the quickstart and the journey in a browser
- [x] T010 Docs: the cart's page, CLAUDE.md, timeline, backlog
- [ ] T011 Merged, closes #370

## Evidence

Verified on 2026-10-09 against the compose stack with Cart and the storefront rebuilt from this branch:

- **Server tests**: `Ecommerce.Cart.Tests` 29 passed, 0 failed, on PostgreSQL (19 before) - `GuestCartTests`: the
  browser's lines read exactly as the same lines in a stored cart (lines, statuses, estimate, checkout answer) and pricing
  writes no cart; the same shape twice is one line; a merge keeps every line, raises to the larger quantity, never
  lowers, and a repeat changes nothing; a merge creates a cart; 51 lines, a quantity of 0 or 1000 refused by both.
  `EndpointAccessTests` passes with the new anonymous action declared.
- **Mutation**: the merge summing quantities instead of taking the larger fails the repeat test. Restored.
- **Client**: 145 files, 799 tests passed - the browser store (one line per shape, reload, unreadable data dropped, 50
  lines, blocked storage), the cart page signed out (priced by the server, never `Cart.get`, checkout offers sign-in,
  changes stay in the browser, empty asks nobody), the merge (once, emptied after, kept on failure, nothing signed out),
  and the product page's add (into the browser; to sign-in only when storage refuses). The old test that a signed-out
  add goes to sign-in was rewritten to the new rule. Without the clearing, the merge test fails. Both apps build.
- **Bruno**: 8 new requests in `cart/` (seq 6-13, "add item again" moved to 14 so checkout sees what it did): pricing
  anonymously, 51 lines 400, a merge, the same merge again, "one line of two, not four", a quantity of 0 400, a merge
  signed out 401, emptied again. The whole collection 719 of 720; the one failure is `my-data/payment`, filed as #364.
- **In a browser, signed out**: Add to cart on the Sony WH-1000XM5 says "Added 1 to your cart", the header's cart shows
  1, nothing redirects; after a reload `/cart` is priced through `POST /api/cart/price` (200) - ₫7,990,000 - with "Sign in
  to check out" and the note that the cart moves to the account; that link opens sign-in reading "Sign in or create an
  account to check out. Your cart comes with you." The test line was removed from the browser afterwards.
- **Not done in a browser**: signing in to watch the merge (an account password typed into the page); the merge is
  covered by the server tests, the component's tests and Bruno's merge requests.
- **Docs**: `docs/reference` regenerated (224 endpoints).
