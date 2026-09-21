# Tasks: Somewhere to Put What You Intend to Buy

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `010-customer-cart`

**Tests are required here**, unlike the last three features: the checkout-outcome transitions are
concurrency and idempotency, which the constitution says need an automated check, and they rest on
a row lock and a guarded flag that only a real database exercises.

---

## Phase 1: Setup

- [ ] T001 Scaffold `server/src/Services/Cart/` as four projects — Domain, Application, Infrastructure, WebApi — mirroring Payment's layout and references. Add all four to `server/Ecommerce.slnx`
- [ ] T002 **Add all four `COPY` lines to `server/Dockerfile` in the same change as the projects.** Feature 009 learned this at publish time; there is no reason to learn it twice
- [ ] T003 [P] Add `postgres-cart` on host **5439** to `server/docker-compose.yml`, and `CART_DB_NAME` / `CART_DB_PORT` defaults in Cart's `Program.cs`

## Phase 2: Foundational

- [ ] T004 Domain: `Cart`, `CartLine`, `CheckoutOutcome` per [data-model](./data-model.md). **No price and no name on `CartLine`**
- [ ] T005 Infrastructure: `CartDbContext`, `IEntityTypeConfiguration` for all three — `carts.UserId` unique, `(CartId, ProductId)` unique, `CHECK (Quantity > 0)`, `xmin` as `carts` concurrency token, `checkout_outcomes.Items` as `jsonb`. Snake_case plural tables
- [ ] T006 Initial migration. **Additive by construction** — a new database
- [ ] T007 `Program.cs`: the house `.env` loader, JWT validation (`AddJwtAuthentication`), `GlobalExceptionHandler`, `/health`, and **both Kestrel endpoints declared together** — REST 8080 and gRPC 8081, with no `app.Run(url)` fallback. Copy Catalog's shape; `ListenAnyIP` replaces `ASPNETCORE_URLS`
- [ ] T008 MassTransit with `SetEndpointNameFormatter(prefix: "CartSvc")`. **Without the prefix, Cart's `OrderCompletedConsumer` shares a queue with Inventory's and Order's** and the three compete for one copy of the event

## Phase 3: US1 — A customer keeps a cart (P1) 🎯

- [ ] T009 [US1] Application: add, set quantity, remove, empty, and read — each keyed on `ICurrentUser.Id`, never on a request field
- [ ] T010 [US1] `CartsController` at `/api/cart` per [contract](./contracts/cart-api.md). `[Authorize]` on the class; a customer with no cart reads an **empty** one, not 404
- [ ] T011 [US1] Catalog: add `DescribeProducts` to `catalog_pricing.proto` and implement it — returns what exists **plus** `missing_product_ids`, **never `NOT_FOUND`** ([research D8](./research.md))
- [ ] T012 [US1] Cart Infrastructure: `GrpcCatalogProducts` behind `ICatalogProducts`, calling `DescribeProducts`. When Catalog is unreachable, return lines with prices unavailable rather than failing the read
- [ ] T013 [US1] Read model: `status` per line, `estimatedTotal`, `canCheckOut`, `pricesAvailable`

## Phase 4: US2 — Checking out buys the cart (P1)

- [ ] T014 [US2] Add `Protos/cart_reading.proto` with `CartReading.GetMyCart` — **empty request**, identity from the forwarded token
- [ ] T015 [US2] Cart WebApi: implement `CartReading` with `[Authorize]`, reading the user through `ICurrentUser`
- [ ] T016 [US2] Order: `ICartReader` in Application, `GrpcCartReader` in Infrastructure, **forwarding the incoming `Authorization` header** as gRPC metadata
- [ ] T017 [US2] `SubmitOrderCommand` takes **no items**; the handler reads the caller's cart, refuses an empty one, then prices through the existing path. Refused with 409 when the cart holds a line that cannot be bought
- [ ] T018 [US2] Three consumers — `OrderSubmitted`, `OrderCompleted`, `OrderFailed` — each: insert-if-absent, `SELECT … FOR UPDATE`, act, commit. **Whichever of Submitted/Completed arrives second applies the removal**, guarded by `Applied`
- [ ] T019 [US2] Removal is a **decrement** of the ordered quantity per line, deleting at zero — never "empty the cart"

## Phase 5: US3 & US4 — The cart never charges, and is private

- [ ] T020 [US3] Confirm nothing in Cart stores or returns a price it could be charged by: `grep -rn "Price" server/src/Services/Cart/Ecommerce.Cart.Domain` returns nothing
- [ ] T021 [US4] Every Cart endpoint `[Authorize]`; no route or body field names a user

## Phase 6: Tests — real PostgreSQL

- [ ] T022 `server/tests/Ecommerce.Cart.Tests` against a throwaway database on 5439, following `PaymentTestFixture`
- [ ] T023 **Completed before Submitted** — lines still removed once the submission arrives
- [ ] T024 **Completed twice** — removed once; a line added in between survives
- [ ] T025 **Decrement, not empty** — cart X×5, order X×2 → X×3; Y added during checkout survives
- [ ] T026 **Failed** — nothing removed, even if Submitted arrived
- [ ] T027 One cart per user under a race — two first-ever adds create one cart
- [ ] T028 **Mutation check**: remove the out-of-order branch and confirm T023 fails

## Phase 7: Integration

- [ ] T029 `docker-compose.app.yml`: `cart` service, 5062:8080 and 6062:8081, `CATALOG_GRPC_ADDRESS`; Order gains `CART_GRPC_ADDRESS`
- [ ] T030 Gateway: route **and** cluster for `/api/cart`, and the health rewrite
- [ ] T031 `verify-saga.sh` and `verify-auth.sh`: fill a cart, then check out with an empty body. Keep the fabricated-price assertion — now it proves the cart is not a price channel either
- [ ] T032 `ci.yml`: Cart and its database in `saga-e2e` **and** `auth-smoke` (both submit orders); Cart in `publish`'s seven — now eight
- [ ] T033 Run quickstart scenarios 1–8 against the containerised stack; paste output. **Scenario 5 and 6 especially**

## Phase 8: Polish

- [ ] T034 [P] `CLAUDE.md`: service map row, the second synchronous dependency, the three-event design and why
- [ ] T035 [P] `docs/README.md` and the architecture doc
- [ ] T036 Open the PR; confirm every job green, including both scenarios in `saga-e2e`

## Dependencies

Setup → Foundational → US1 → US2 → US3/US4 → Tests → Integration → Polish. US2 depends on US1 (a cart
must exist to check out from). The tests in Phase 6 cover US2's consumers and can be written as soon
as T018 exists.

**MVP**: Phases 1–4 plus T022–T026. Everything the spec calls P1, and the tests that prove its
timing.
