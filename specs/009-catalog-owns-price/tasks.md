# Tasks: The Shop Decides What Things Cost

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `009-catalog-owns-price`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**The negative control is free, and it expires when this merges.** `main` accepts a fabricated
price today, so the regression scenario can be run against `main` and **must fail there**. Use it
while it costs nothing; afterwards proving the check can fail needs a scratch branch.

**Tests**: the 61 existing ones must keep passing, and some of them will need changing — several
construct `OrderItemRequest` with a price. That is expected and is itself a signal: a test that
cannot compile after FR-003 was a test that relied on the defect.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 **Test whether `Http1AndHttp2` works on a plaintext endpoint in .NET 10, before writing anything.** [research D3](./research.md) specifies a *second* port on the strength of Microsoft's documented behaviour, which was **not verified**. If one port can serve both, the whole port story collapses to a one-line change and T005–T007 mostly disappear. Ten minutes, and it decides the shape of the feature
- [ ] T002 [P] Record the baseline for SC-008: time from `POST /api/orders` to a response, on `main`, five runs. Checkout is about to gain a network round trip and "it felt fine" is not a measurement
- [ ] T003 [P] Bring the stack up and reproduce [#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18) once, so the control is known-good before anything changes. [quickstart](./quickstart.md) scenario 1 against `main` — it must **succeed** in selling a 40,000,000 item for 1

---

## Phase 2: Foundational

**Blocking.** The contract and the transport have to exist before either side can be written.

- [ ] T004 Create `server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/` with `Protos/catalog_pricing.proto` per [contracts/catalog-pricing-grpc.md](./contracts/catalog-pricing-grpc.md). **Price is a `string`, not a `double`** — protobuf has no decimal, money is `decimal(18,2)` by constitution, and a double cannot represent `0.1`. **No availability field**, deliberately ([data-model](./data-model.md)). Add the project to `Ecommerce.slnx`
- [ ] T005 Configure Catalog's Kestrel with a **second** endpoint on container port 8081, `HttpProtocols.Http2`, in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs`. Note the existing `app.Run("http://localhost:5057")` / `ASPNETCORE_URLS` fallback at the end of that file — the second endpoint has to survive **both** paths, and the `start-dev` path is the one that will be forgotten
- [ ] T006 Add `Grpc.AspNetCore`, `MapGrpcService<CatalogPricingService>()` and `AddGrpcHealthChecks()`/`MapGrpcHealthChecksService()` to Catalog
- [ ] T007 Publish `6057:8081` for Catalog in `server/docker-compose.app.yml`. **Leave the container healthcheck pointing at REST `/health`** — `curl` cannot speak gRPC health, so repointing it would leave a green probe that tests nothing ([research D8](./research.md))
- [ ] T008 Create `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/DependencyUnavailableException.cs` and map it to **503** in `GlobalExceptionHandler`. Without it, refusing because Catalog is down answers **500** — *we are broken* — when the truth is *try again shortly*. `Ecommerce.Shared/Exceptions/` currently holds exactly `NotFoundException` and `ConflictException`; this touches a building block all seven services use

**Checkpoint**: Catalog answers on two ports, and the system can express "a dependency is unavailable".

---

## Phase 3: User Story 1 — A customer pays what the shop charges (P1) 🎯 MVP

**Goal**: the price charged comes from Catalog, never from the request.

**Independent test**: [quickstart](./quickstart.md) scenarios 1, 2, 3.

- [ ] T009 [US1] Implement `CatalogPricingService` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/`. One call resolves **every** requested id. Return `NOT_FOUND` when any id is unknown; return `OK` with `sellable: false` for a product that exists but is inactive — *not for sale* is an answer, not a failure
- [ ] T010 [US1] Declare `ICatalogPrices` in `server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/`. **Application must not reference any gRPC package** — Principle II says abstractions only, never a transport package, and this is checkable by grep rather than by intention
- [ ] T011 [US1] Implement `GrpcCatalogPrices` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Catalog/`, registered through `AddGrpcClient` in Infrastructure's `DependencyInjection.cs`. Same shape as `IPaymentGateway` / `StubPaymentGateway`, which is what makes swapping gRPC for REST later a one-file change
- [ ] T012 [US1] Map the four outcomes to four distinct results: priced; `NOT_FOUND` → `NotFoundException`; not sellable → `ConflictException`; `UNAVAILABLE`/`DEADLINE_EXCEEDED` → `DependencyUnavailableException`. **`NOT_FOUND` and `UNAVAILABLE` must never collapse** — a customer told "no such product" about a catalogue that is merely offline goes and checks a catalogue that is fine
- [ ] T013 [US1] Call the lookup in `SubmitOrderCommandHandler` **before anything is staged**, and build the order lines from what comes back. Steps 4–6 (stage → publish → `SaveChangesAsync` once) are untouched. **Do not put the call between the publish and the save** — Principle III is non-negotiable and a slow Catalog there would widen the window between staging and commit
- [ ] T014 [US1] Remove `ProductName` and `UnitPrice` from `OrderItemRequest`, and drop the corresponding rules from `SubmitOrderCommandValidator`. **Removed, not ignored** (FR-003) — a field the server accepts and discards reads as supported in every client that sees it, which is exactly why `UserId` was deleted rather than left in place
- [ ] T015 [US1] Fix the existing tests that construct `OrderItemRequest` with a price. Each one that stops compiling is a test that was asserting against a number it supplied itself — replace the assertion with the catalogue price, do not reintroduce the field to keep them green
- [ ] T016 [US1] Run [quickstart](./quickstart.md) scenarios 1, 2 and 3 and paste the output. Scenario 1 uses **raw JSON with the extra field** — once the type has no such property, sending a typed object tests the type system rather than the server

**Checkpoint**: the hole in #18 is closed. This is the MVP.

---

## Phase 4: User Story 2 — What somebody bought does not change (P1)

**Goal**: an order line is a record, not a reference.

**Independent test**: [quickstart](./quickstart.md) scenario 4.

- [ ] T017 [US2] Confirm the looked-up name and price are **written to the order line**, not resolved at read time. This is the half that passes every test written for US1 and is still wrong: fetching the right price and storing a reference satisfies scenarios 1–3 completely and rewrites history the next time the catalogue is edited
- [ ] T018 [US2] Run [quickstart](./quickstart.md) scenario 4: place an order, change the price **and** rename the product in Catalog, read the order back. Line price, total and name all unchanged. Paste before and after
- [ ] T019 [US2] Extend scenario 4: confirm the order is still readable in full after the product is **deactivated**, and after it is **deleted**. This is why the name is copied as well as the price — an order whose lines cannot be described is not a record of anything

**Checkpoint**: what was paid stays what was paid.

---

## Phase 5: User Story 3 — The hole cannot reopen unnoticed (P1)

**Goal**: something goes red if a fabricated price is ever accepted again.

**Independent test**: [quickstart](./quickstart.md) scenarios 5, 6, 7, 8, 9.

**Do not defer T022.** Proving the check *refuses* when Catalog cannot answer is the branch whose absence turns the feature into theatre — a lookup that falls back to the request on failure is worse than no lookup, because it looks safe.

- [ ] T020 [US3] Run [quickstart](./quickstart.md) scenario 5 in both shapes: one unknown line → 404; **two lines where only the second is unknown → refused entirely**, no order row and no reservation. Partial acceptance is worse than refusal
- [ ] T021 [US3] Run [quickstart](./quickstart.md) scenario 6: an inactive product is refused with **409**, distinguishable at a glance from scenario 5's 404
- [ ] T022 [US3] **Negative control — Catalog unreachable.** `docker stop ecommerce-catalog`, submit an order. Confirm: zero orders created; **503**, not 500 and not 404; the message names the *lookup* rather than the product; and it gives up after the documented attempts rather than hanging. Paste it
- [ ] T023 [US3] Run [quickstart](./quickstart.md) scenario 8 — inspect the request type and confirm neither field exists
- [ ] T024 [US3] Add the fabricated-price scenario to `.github/scripts/verify-saga.sh`, sending raw JSON. Follow the existing conventions: `set -euo pipefail`, the probed Python interpreter, `fail()`/`pass()`, and `git update-index --chmod=+x` is **not** needed since the file already has its mode
- [ ] T025 [US3] **Run the new scenario against `main` and confirm it FAILS.** The unfixed system is the control and it costs nothing today. Paste both directions: red on `main`, green on this branch. A check that has never been red proves nothing

**Checkpoint**: the defect cannot return quietly.

---

## Phase 6: CI and containers

- [ ] T026 Confirm the `.proto` reaches the image — check `server/.dockerignore` does not exclude it, and that `docker build` produces working generated code for both Catalog and Order
- [ ] T027 Add Catalog's gRPC port to the `saga-e2e` job if anything there needs it, and confirm `verify-saga.sh`'s new scenario runs in CI with `SAGA_E2E_REQUIRE_ALL=1`
- [ ] T028 Open a pull request and confirm every job is green, including the new scenario

---

## Phase 7: Polish & Cross-Cutting

- [ ] T029 Fill in SC-008: the before and after from T002. If the round trip is **not** invisible, say so — that is the evidence for the priced-cart shape in [#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19), not a reason to hide the number
- [ ] T030 [P] Update `CLAUDE.md`: Catalog's second port in the service map, the first synchronous call and what it costs (Catalog down now stops orders), and that `OrderItemRequest` no longer carries a price — beside the existing note that it never carried a `UserId`
- [ ] T031 [P] Update `docs/architecture/service-to-service-communication.md` — it currently describes this as a decision *about to be made*. Record what was chosen, and whether T001 changed it
- [ ] T032 [P] Update `docs/README.md` if it lists ports or describes checkout
- [ ] T033 Confirm `Order.Application` references no gRPC package: `grep -r "Grpc" server/src/Services/Order/Ecommerce.Order.Application/` must be empty. Principle II, checkable rather than intended

---

## Dependencies

```text
Phase 1 (T001-T003)         ← T001 can collapse Phase 2
   └─> Phase 2 (T004-T008)  ← contract, transport, and the 503 exception
          └─> Phase 3 US1 (T009-T016)   ← MVP: the hole is closed
                 ├─> Phase 4 US2 (T017-T019)   ← independent of US3
                 └─> Phase 5 US3 (T020-T025)
                        └─> Phase 6 (T026-T028)
                               └─> Phase 7 (T029-T033)
```

- **T001 is first because it can rewrite Phase 2.** If `Http1AndHttp2` serves h2c on a plaintext
  endpoint, there is no second port, no compose change, and no new entry in the service map.
- **US2 and US3 are independent of each other** and both depend on US1.
- **T008 is in Phase 2, not Phase 7.** The 503 exception is not polish: without it every dependency
  failure answers 500 and pages somebody.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T002, T003 |
| 2 | T004 then T005–T007 (one file each); T008 is independent |
| 3 | T009 and T010 are different services; T011–T014 are sequential |
| 4, 5 | each scenario needs a real run, in order |
| 7 | T030, T031, T032 |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 16 tasks, and it closes #18 on its own.

Then Phase 4 (the half that would rewrite history), Phase 5 (proof it can fail), Phase 6, Phase 7.

**The three tasks most likely to be skipped, and why not to**: **T001**, because it feels like
preamble and it can delete a third of the work; **T022**, because Catalog being down feels like
somebody else's problem until a fallback quietly reintroduces the defect; and **T025**, because
running a new check against `main` to watch it fail feels redundant — and it is the only moment when
that control is free.
