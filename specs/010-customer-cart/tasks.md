# Tasks: Somewhere to Put What You Intend to Buy

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `010-customer-cart`

**Tests are required here**, unlike the last three features: the checkout-outcome transitions are
concurrency and idempotency, which the constitution says need an automated check, and they rest on
a row lock and a guarded flag that only a real database exercises.

---

## Phase 1: Setup

- [X] T001 Scaffold `server/src/Services/Cart/` as four projects — Domain, Application, Infrastructure, WebApi — mirroring Payment's layout and references. Add all four to `server/Ecommerce.slnx`
- [X] T002 **Add all four `COPY` lines to `server/Dockerfile` in the same change as the projects.** Feature 009 learned this at publish time; there is no reason to learn it twice
- [X] T003 [P] Add `postgres-cart` on host **5439** to `server/docker-compose.yml`, and `CART_DB_NAME` / `CART_DB_PORT` defaults in Cart's `Program.cs`

## Phase 2: Foundational

- [X] T004 Domain: `Cart`, `CartLine`, `CheckoutOutcome` per [data-model](./data-model.md). **No price and no name on `CartLine`**
- [X] T005 Infrastructure: `CartDbContext`, `IEntityTypeConfiguration` for all three — `carts.UserId` unique, `(CartId, ProductId)` unique, `CHECK (Quantity > 0)`, ~~`xmin` as `carts` concurrency token~~ *(replaced by `FOR UPDATE` — see below)*, `checkout_outcomes.Items` as `jsonb`. Snake_case plural tables
- [X] T006 Initial migration. **Additive by construction** — a new database
- [X] T007 `Program.cs`: the house `.env` loader, JWT validation (`AddJwtAuthentication`), `GlobalExceptionHandler`, `/health`, and **both Kestrel endpoints declared together** — REST 8080 and gRPC 8081, with no `app.Run(url)` fallback. Copy Catalog's shape; `ListenAnyIP` replaces `ASPNETCORE_URLS`
- [X] T008 MassTransit with `SetEndpointNameFormatter(prefix: "CartSvc")`. **Without the prefix, Cart's `OrderCompletedConsumer` shares a queue with Inventory's and Order's** and the three compete for one copy of the event

## Phase 3: US1 — A customer keeps a cart (P1) 🎯

- [X] T009 [US1] Application: add, set quantity, remove, empty, and read — each keyed on `ICurrentUser.Id`, never on a request field
- [X] T010 [US1] `CartsController` at `/api/cart` per [contract](./contracts/cart-api.md). `[Authorize]` on the class; a customer with no cart reads an **empty** one, not 404
- [X] T011 [US1] Catalog: add `DescribeProducts` to `catalog_pricing.proto` and implement it — returns what exists **plus** `missing_product_ids`, **never `NOT_FOUND`** ([research D8](./research.md))
- [X] T012 [US1] Cart Infrastructure: `GrpcCatalogProducts` behind `ICatalogProducts`, calling `DescribeProducts`. When Catalog is unreachable, return lines with prices unavailable rather than failing the read
- [X] T013 [US1] Read model: `status` per line, `estimatedTotal`, `canCheckOut`, `pricesAvailable`

## Phase 4: US2 — Checking out buys the cart (P1)

- [X] T014 [US2] Add `Protos/cart_reading.proto` with `CartReading.GetMyCart` — **empty request**, identity from the forwarded token
- [X] T015 [US2] Cart WebApi: implement `CartReading` with `[Authorize]`, reading the user through `ICurrentUser`
- [X] T016 [US2] Order: `ICartReader` in Application, `GrpcCartReader` in Infrastructure, **forwarding the incoming `Authorization` header** as gRPC metadata
- [X] T017 [US2] `SubmitOrderCommand` takes **no items**; the handler reads the caller's cart, refuses an empty one, then prices through the existing path. Refused with 409 when the cart holds a line that cannot be bought
- [X] T018 [US2] Three consumers — `OrderSubmitted`, `OrderCompleted`, `OrderFailed` — each: insert-if-absent, `SELECT … FOR UPDATE`, act, commit. **Whichever of Submitted/Completed arrives second applies the removal**, guarded by `Applied`
- [X] T019 [US2] Removal is a **decrement** of the ordered quantity per line, deleting at zero — never "empty the cart"

## Phase 5: US3 & US4 — The cart never charges, and is private

- [X] T020 [US3] Confirm nothing in Cart stores or returns a price it could be charged by: `grep -rn "Price" server/src/Services/Cart/Ecommerce.Cart.Domain` returns nothing
- [X] T021 [US4] Every Cart endpoint `[Authorize]`; no route or body field names a user

## Phase 6: Tests — real PostgreSQL

- [X] T022 `server/tests/Ecommerce.Cart.Tests` against a throwaway database on 5439, following `PaymentTestFixture`
- [X] T023 **Completed before Submitted** — lines still removed once the submission arrives
- [X] T024 **Completed twice** — removed once; a line added in between survives
- [X] T025 **Decrement, not empty** — cart X×5, order X×2 → X×3; Y added during checkout survives
- [X] T026 **Failed** — nothing removed, even if Submitted arrived
- [X] T027 One cart per user under a race — two first-ever adds create one cart
- [X] T028 **Mutation check**: remove the out-of-order branch and confirm T023 fails

## Phase 7: Integration

- [X] T029 `docker-compose.app.yml`: `cart` service, 5062:8080 and 6062:8081, `CATALOG_GRPC_ADDRESS`; Order gains `CART_GRPC_ADDRESS`
- [X] T030 Gateway: route **and** cluster for `/api/cart`, and the health rewrite
- [X] T031 `verify-saga.sh` and `verify-auth.sh`: fill a cart, then check out with an empty body. Keep the fabricated-price assertion — now it proves the cart is not a price channel either
- [X] T032 `ci.yml`: Cart and its database in `saga-e2e` **and** `auth-smoke` (both submit orders); Cart in `publish`'s seven — now eight
- [X] T033 Run quickstart scenarios 1–8 against the containerised stack; paste output. **Scenario 5 and 6 especially**

## Phase 8: Polish

- [X] T034 [P] `CLAUDE.md`: service map row, the second synchronous dependency, the three-event design and why
- [X] T035 [P] `docs/README.md` and the architecture doc
- [ ] T036 Open the PR; confirm every job green, including both scenarios in `saga-e2e`

## Dependencies

Setup → Foundational → US1 → US2 → US3/US4 → Tests → Integration → Polish. US2 depends on US1 (a cart
must exist to check out from). The tests in Phase 6 cover US2's consumers and can be written as soon
as T018 exists.

**MVP**: Phases 1–4 plus T022–T026. Everything the spec calls P1, and the tests that prove its
timing.

## What actually happened

Recorded after implementation, because several of these contradicted the plan or found defects that
predate this feature.

- **`xmin` was not used (T005).** Writes take `SELECT … FOR UPDATE` on the cart row instead, the
  pattern Inventory already uses. It serialises writes to one cart outright — no conflict to detect,
  no retry loop. First creation is `INSERT … ON CONFLICT DO NOTHING` then the same lock; 20
  concurrent first adds produce one cart. [data-model](./data-model.md) is updated.
- **All eight consumer tests failed first time with `DbUpdateConcurrencyException`.** A `Guid` key
  defaults to `ValueGeneratedOnAdd`; a new `CartLine` found through the `Lines` navigation with its
  id already set was taken for an existing row and `UPDATE`d, affecting zero rows. Fixed with
  `ValueGeneratedNever()` on `Cart.Id` and `CartLine.Id` — a model change with no schema change.
- **Every request to the Cart container returned 401 (T033).** `IDX10208: Unable to validate
  audience` — Cart had no `appsettings.json`, so `JwtSettings` was empty. Added. The service started
  and reported healthy throughout; `AddJwtAuthentication` does not fail at startup on missing
  settings, which the constitution requires. **Not fixed here** — it is a shared building block and
  deserves its own change.
- **Validation had never run for any command returning nothing (found in T033).** Adding a quantity
  of `-1` returned 204 despite `GreaterThan(0)`. The shared `ValidationBehavior` was constrained to
  `TRequest : IRequest<TResponse>`; MediatR 12's void commands implement `IRequest`, the behavior
  could not be closed over them, and it was skipped silently. A failing test was written first
  (`ValidationTests`), then the constraint was widened to `notnull`. This affects **every service**,
  not just Cart — any void command's validator in the repository had been dead code.
- **T028, the mutation check, found a weaker test than intended.** Removing the out-of-order branch
  fails exactly `Completion_BEFORE_submission_still_removes_them`. The concurrent
  submit-and-complete test does **not** catch it: `Submitted` tends to win the lock, so the race
  rarely exercises the branch. It stays as a test of the lock, not of the ordering.
- **A checkpoint commit was made before mutating code for T028**, after feature 009 lost three files
  to a `git checkout` of uncommitted work. It is squashed into the feature commit.

### Verified against the containerised stack

```text
queues: OrderCompleted (Inventory), OrderSvcOrderCompleted, CartSvcOrderCompleted — 1 consumer each
verify-saga approve : added 3, cart holds 3, body "99 @ 0.01" ignored, charged 29.97,
                      Completed, stock 50 -> 47, cart emptied            5/5 assertions
verify-saga reject  : order Failed, cart still holds 3                  5/5 assertions
verify-auth         : pass
scenario 1 merge    : X x3 on one line, estimatedTotal 34.97, same cart on a fresh token
scenario 2          : PUT qty 5 -> 204, DELETE -> 204, qty -1 -> 400, qty 0 -> 400 (add) / removes (PUT)
scenario 7 price    : cart 9.99; catalogue set to 50; cart shows 50; checkout charged 50.00
scenario 8 privacy  : other customer sees 0 lines; anonymous 401; ?userId= ignored
empty cart checkout : 409
dotnet test         : 71/71 (Cart 10, Payment 13, Order 15, Catalog 8, Inventory 25)
```
