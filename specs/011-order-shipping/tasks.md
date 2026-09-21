# Tasks: Somewhere for the Order to Go

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `011-order-shipping`

**Tests are required.** The one-default invariant, the address limit, ownership and the fulfilment
guards are database guarantees (constitution V), and Identity has never had a test project.

---

## Phase 1: Setup

- [X] T001 Add `server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/address_reading.proto` per [contracts](./contracts/api.md); register it `GrpcServices="Both"` in `Ecommerce.Contracts.Grpc.csproj`
- [X] T002 Reference `Ecommerce.Contracts.Grpc` and `Grpc.AspNetCore` (+ health checks, reflection, same versions as Catalog) from `Ecommerce.Identity.WebApi`
- [X] T003 Create `server/tests/Ecommerce.Identity.Tests` (xUnit, `<Using Include="Xunit" />`, throwaway DB on 5435 following `CartTestFixture`); add to `server/Ecommerce.slnx`

## Phase 2: Foundational

- [X] T004 Identity `Program.cs`: declare **both** Kestrel endpoints (HTTP/1.1 on the `ASPNETCORE_URLS` port or 5056; HTTP/2 on `IDENTITY_GRPC_PORT`, default 5156), remove the `app.Run(url)` fallback, add `AddGrpc`, gRPC health and reflection (research D5)
- [X] T005 Order `OrderStatus`: add `Preparing = 8`, `Shipped = 9`; `Order` gains owned `ShippingAddress` and `ShippingOptionCode/Name`, `ShippingPrice`, `TrackingReference`; configure in `OrderConfiguration.cs` per [data-model](./data-model.md)
- [X] T006 Order migration `AddShippingToOrders`: nullable columns **plus** `UPDATE orders SET "Status" = 'Paid' WHERE "Status" = 'Completed'`. Confirm it is additive (no drop/rename/narrow)

## Phase 3: User Story 1 — Address book (P1)

- [X] T007 [US1] Identity Domain `DeliveryAddress`; `DeliveryAddressConfiguration` (`delivery_addresses`, `ValueGeneratedNever()`, FK cascade, **partial unique index on `UserId` WHERE `IsDefault`**)
- [X] T008 [US1] Identity migration `AddDeliveryAddresses` (additive — a new table)
- [X] T009 [US1] `IAddressRepository` + `AddressRepository`: every write first `SELECT 1 FROM users WHERE "Id" = @me FOR UPDATE` (research D10); list/get always filtered by `UserId`
- [X] T010 [US1] Commands `SaveAddress` (first → default; 20-limit → `ConflictException`), `UpdateAddress`, `DeleteAddress` (promote newest remaining if default), `SetDefaultAddress`; queries `GetMyAddresses`, `GetMyAddress`. Every one reads the user from `ICurrentUser`; not-mine → `NotFoundException`
- [X] T011 [US1] `AddressValidator` per research D11 — messages say "not well-formed", never "invalid address"
- [X] T012 [US1] `AddressesController` at `/api/addresses`, `[Authorize]`, per contracts
- [X] T013 [US1] Gateway: `/api/addresses` **and** `/api/addresses/{**catch-all}` routes → identity-cluster

## Phase 4: User Story 2 + 5 — Checkout to an address, charged for delivery; nobody else's address (P1)

- [X] T014 [US2] Identity `AddressReadingService` (`[Authorize]`, empty identity in the request, `found=false` for missing / not-mine / no default)
- [X] T015 [US2] Order: `IAddressReader` + `GrpcAddressReader` — forwards the caller's `Authorization` header, 3 attempts 200 ms / 1 s, `Unauthenticated` → 401, `Unavailable`/`DeadlineExceeded` → `DependencyUnavailableException`; `IDENTITY_GRPC_ADDRESS` default `http://localhost:5156`
- [X] T016 [US2] Order: `IShippingOptions` + `ConfiguredShippingOptions` from `Shipping:Options`, validated at startup (≥1, unique codes, price ≥ 0); defaults standard 5.00 / express 15.00
- [X] T017 [US2] `SubmitOrderCommand(Guid? AddressId, string ShippingOption)` — **no user field**; validator requires `ShippingOption`. Handler: cart → address → prices → option, **all before staging**; unknown option → 400; missing address → 404 / no default → 409; copy the address and option onto the order; `TotalAmount` = lines + shipping
- [X] T018 [US2] `GET /api/orders/shipping-options` `[AllowAnonymous]`; order responses gain `shippingAddress`, `shippingOption`, `shippingPrice`, `trackingReference`
- [X] T019 [US2] `docker-compose.app.yml`: Identity `IDENTITY_GRPC_PORT: "8081"`, `6056:8081`; Order `IDENTITY_GRPC_ADDRESS: http://identity:8081`

## Phase 5: User Story 3 — What an order was sent to never changes (P1)

- [X] T020 [US3] Confirm no `AddressId` column exists on `orders` and no read path joins the address book (grep in the PR description)

## Phase 6: User Story 4 — Fulfilment (P2)

- [X] T021 [US4] `CompleteOrderCommandHandler`: settle to `Paid` (guard unchanged); order reads report a legacy `Completed` as `Paid`
- [X] T022 [US4] `IOrderRepository.TryAdvanceAsync(id, from, to, tracking?)` — one guarded `ExecuteUpdate` (research D9)
- [X] T023 [US4] Commands `PrepareOrder`, `ShipOrder` (tracking required) implementing the repeat rules; query `GetOrdersForFulfilment(status, page, pageSize)`
- [X] T024 [US4] `OrdersController`: `POST {id}/preparing`, `POST {id}/shipment`, `GET fulfilment` — `[Authorize(Roles = "Admin")]`

## Phase 7: Tests

- [X] T025 [P] Identity.Tests: first address is default; set-default leaves exactly one; deleting the default promotes another; 21st address refused; **20 concurrent set-defaults → exactly one default**
- [X] T026 [P] Identity.Tests: another user's address is `NotFound` for get, update, delete, set-default — same exception as a random id
- [X] T027 [P] Identity.Tests: validation rejects each malformed field and accepts an odd-but-plausible postcode
- [X] T028 [P] Order.Tests: completion settles to `Paid`; `preparing` then `shipment`; repeats are no-ops; shipment with a different reference → conflict; `preparing` on `Shipped`, `Failed`, `Submitted` → conflict, row unchanged
- [X] T029 [P] Order.Tests: checkout freezes the address and option; `TotalAmount` includes shipping; unknown option refused; no default → conflict (fake `IAddressReader`)
- [X] T030 **Negative control**: remove the fulfilment `WHERE "Status" = @from` guard; confirm T028's "preparing a Shipped order" fails; restore

## Phase 8: Integration

- [X] T031 `ci.yml`: Identity.Tests needs `postgres-identity` in `build`; `IDENTITY_GRPC_*` env in both smoke jobs; Order started with `IDENTITY_GRPC_ADDRESS`
- [X] T032 `verify-saga.sh`: save an address; checkout with express; assert the copied address, `shippingPrice`, and the **payment amount** = items + express; assert `Paid` (was `Completed`); Admin → Preparing → Shipped; read back as the customer. Reject branch: `Failed`, and fulfilment refused
- [X] T033 `verify-auth.sh`: checkout now needs a body; two real customers — B cannot get/put/delete A's address (`404`) nor check out with it (`404`)
- [X] T034 Bruno: `addresses/` folder; checkout body; `shipping-options`; admin fulfilment; security checks for cross-customer addresses; `Completed` → `Paid`
- [X] T035 Run quickstart scenarios 1–22 against containers; paste output

## Phase 9: Polish

- [X] T036 [P] `specs/003-order-lifecycle/data-model.md`: `Paid` reachable, `Completed` no longer written — **change the decision, not around it**
- [X] T037 [P] `CLAUDE.md` (service map: Identity 5056 + **6056 gRPC**; checkout's third synchronous dependency; fulfilment), `docs/architecture/microservices-design.md`, `service-to-service-communication.md`, `jwt-setup.md` access table, `docs/README.md` ports
- [ ] T038 Open the PR (`Closes #20`); every CI job green, both saga branches

## Dependencies

Setup → Foundational → US1 → US2/US5 → US3 → US4 → Tests → Integration → Polish. US2 depends on US1
(an address must exist to ship to). US4 depends only on Foundational and can be built in parallel
with US1–US3.

**MVP**: Phases 1–5 plus T025–T027, T029 — addresses, checkout to them, charged for delivery, and the
ownership proof.

## What actually happened

- **Identity had no test project.** `Ecommerce.Identity.Tests` is its first: 16 tests against a real
  database on 5435, including 20 concurrent set-defaults and 10 concurrent first saves, each ending
  with exactly one default.
- **"Exactly one default" needed two saves, not one.** Demoting the old default and promoting the new
  one in a single `SaveChanges` lets EF order the `UPDATE`s either way, and the partial unique index
  refuses the moment two rows are default. Both handlers save twice inside one transaction, under the
  owner's row lock.
- **Negative control (T030)**: removing the fulfilment guard fails **6** of the 10 fulfilment tests,
  including "preparing a Shipped order"; restored, all pass.
- **A test was wrong, not the code.** The Bruno check that the new order sits in the fulfilment queue
  failed: the queue is oldest-first and earlier runs had left many `Paid` orders ahead of it. It now
  asserts what the queue promises - everything in it is `Paid` - rather than where one order lands.
- **`OrderStatus` kept `Completed`.** Nothing writes it; reads report it as `Paid`, and the migration
  rewrites existing rows. Removing the value would strand an earlier image reading a row it wrote.
- **Order ids moved to UUID v7** in the checkout handler while it was open - one of the three
  `NewGuid()` call sites ADR-001 lists.

### Verified against the containerised stack

```text
verify-saga approve : address saved, express = 15.0, charged 44.97 (3 x 9.99 + 15.00), recipient
                      copied, Paid, stock 50 -> 47, cart emptied, Payment asked for 44.97,
                      Admin -> Preparing -> Shipped with reference, address edited -> order unchanged
verify-saga reject  : Failed, cart untouched, preparing a Failed order -> 409
verify-auth         : B cannot GET/PUT/DELETE A's address (404 x3, same as a random id);
                      B checking out with A's address -> 404; A's address untouched
Identity stopped    : checkout -> 503, 0 orders placed
Bruno               : 51 requests, 50 pass; the one failure is #28 (duplicate registration 500)
dotnet test         : 103/103 (Identity 16, Order 31, Cart 10, Payment 13, Catalog 8, Inventory 25)
```
