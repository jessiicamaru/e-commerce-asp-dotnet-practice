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

- [X] T001 **Test whether `Http1AndHttp2` works on a plaintext endpoint in .NET 10, before writing anything.** [research D3](./research.md) specifies a *second* port on the strength of Microsoft's documented behaviour, which was **not verified**. If one port can serve both, the whole port story collapses to a one-line change and T005–T007 mostly disappear. Ten minutes, and it decides the shape of the feature
- [X] T002 [P] Record the baseline for SC-008: time from `POST /api/orders` to a response, on `main`, five runs. Checkout is about to gain a network round trip and "it felt fine" is not a measurement
- [X] T003 [P] Bring the stack up and reproduce [#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18) once, so the control is known-good before anything changes. [quickstart](./quickstart.md) scenario 1 against `main` — it must **succeed** in selling a 40,000,000 item for 1

---

## Phase 2: Foundational

**Blocking.** The contract and the transport have to exist before either side can be written.

- [X] T004 Create `server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/` with `Protos/catalog_pricing.proto` per [contracts/catalog-pricing-grpc.md](./contracts/catalog-pricing-grpc.md). **Price is a `string`, not a `double`** — protobuf has no decimal, money is `decimal(18,2)` by constitution, and a double cannot represent `0.1`. **No availability field**, deliberately ([data-model](./data-model.md)). Add the project to `Ecommerce.slnx`
- [X] T005 Configure Catalog's Kestrel with a **second** endpoint on container port 8081, `HttpProtocols.Http2`, in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs`. Note the existing `app.Run("http://localhost:5057")` / `ASPNETCORE_URLS` fallback at the end of that file — the second endpoint has to survive **both** paths, and the `start-dev` path is the one that will be forgotten
- [X] T006 Add `Grpc.AspNetCore`, `MapGrpcService<CatalogPricingService>()` and `AddGrpcHealthChecks()`/`MapGrpcHealthChecksService()` to Catalog
- [X] T007 Publish `6057:8081` for Catalog in `server/docker-compose.app.yml`. **Leave the container healthcheck pointing at REST `/health`** — `curl` cannot speak gRPC health, so repointing it would leave a green probe that tests nothing ([research D8](./research.md))
- [X] T008 Create `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/DependencyUnavailableException.cs` and map it to **503** in `GlobalExceptionHandler`. Without it, refusing because Catalog is down answers **500** — *we are broken* — when the truth is *try again shortly*. `Ecommerce.Shared/Exceptions/` currently holds exactly `NotFoundException` and `ConflictException`; this touches a building block all seven services use

**Checkpoint**: Catalog answers on two ports, and the system can express "a dependency is unavailable".

---

## Phase 3: User Story 1 — A customer pays what the shop charges (P1) 🎯 MVP

**Goal**: the price charged comes from Catalog, never from the request.

**Independent test**: [quickstart](./quickstart.md) scenarios 1, 2, 3.

- [X] T009 [US1] Implement `CatalogPricingService` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/`. One call resolves **every** requested id. Return `NOT_FOUND` when any id is unknown; return `OK` with `sellable: false` for a product that exists but is inactive — *not for sale* is an answer, not a failure
- [X] T010 [US1] Declare `ICatalogPrices` in `server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/`. **Application must not reference any gRPC package** — Principle II says abstractions only, never a transport package, and this is checkable by grep rather than by intention
- [X] T011 [US1] Implement `GrpcCatalogPrices` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Catalog/`, registered through `AddGrpcClient` in Infrastructure's `DependencyInjection.cs`. Same shape as `IPaymentGateway` / `StubPaymentGateway`, which is what makes swapping gRPC for REST later a one-file change
- [X] T012 [US1] Map the four outcomes to four distinct results: priced; `NOT_FOUND` → `NotFoundException`; not sellable → `ConflictException`; `UNAVAILABLE`/`DEADLINE_EXCEEDED` → `DependencyUnavailableException`. **`NOT_FOUND` and `UNAVAILABLE` must never collapse** — a customer told "no such product" about a catalogue that is merely offline goes and checks a catalogue that is fine
- [X] T013 [US1] Call the lookup in `SubmitOrderCommandHandler` **before anything is staged**, and build the order lines from what comes back. Steps 4–6 (stage → publish → `SaveChangesAsync` once) are untouched. **Do not put the call between the publish and the save** — Principle III is non-negotiable and a slow Catalog there would widen the window between staging and commit
- [X] T014 [US1] Remove `ProductName` and `UnitPrice` from `OrderItemRequest`, and drop the corresponding rules from `SubmitOrderCommandValidator`. **Removed, not ignored** (FR-003) — a field the server accepts and discards reads as supported in every client that sees it, which is exactly why `UserId` was deleted rather than left in place
- [X] T015 [US1] ~~Fix the existing tests that construct `OrderItemRequest` with a price.~~ **There are none.** `grep -rln SubmitOrder tests/` returns nothing: no test exercises order submission at all, so removing the fields broke nothing and the solution built first time. The fifteen Order tests are six query tests and nine settlement tests, building `OrderItem` entities directly. The task's premise was wrong and the truth is worse — this path has never had a test
- [X] T016 [US1] Run [quickstart](./quickstart.md) scenarios 1, 2 and 3 and paste the output. Scenario 1 uses **raw JSON with the extra field** — once the type has no such property, sending a typed object tests the type system rather than the server

**Checkpoint**: the hole in #18 is closed. This is the MVP.

---

## Phase 4: User Story 2 — What somebody bought does not change (P1)

**Goal**: an order line is a record, not a reference.

**Independent test**: [quickstart](./quickstart.md) scenario 4.

- [X] T017 [US2] Confirm the looked-up name and price are **written to the order line**, not resolved at read time. This is the half that passes every test written for US1 and is still wrong: fetching the right price and storing a reference satisfies scenarios 1–3 completely and rewrites history the next time the catalogue is edited
- [X] T018 [US2] Run [quickstart](./quickstart.md) scenario 4: place an order, change the price **and** rename the product in Catalog, read the order back. Line price, total and name all unchanged. Paste before and after
- [X] T019 [US2] Extend scenario 4: confirm the order is still readable in full after the product is **deactivated**, and after it is **deleted**. This is why the name is copied as well as the price — an order whose lines cannot be described is not a record of anything

**Checkpoint**: what was paid stays what was paid.

---

## Phase 5: User Story 3 — The hole cannot reopen unnoticed (P1)

**Goal**: something goes red if a fabricated price is ever accepted again.

**Independent test**: [quickstart](./quickstart.md) scenarios 5, 6, 7, 8, 9.

**Do not defer T022.** Proving the check *refuses* when Catalog cannot answer is the branch whose absence turns the feature into theatre — a lookup that falls back to the request on failure is worse than no lookup, because it looks safe.

- [X] T020 [US3] Run [quickstart](./quickstart.md) scenario 5 in both shapes: one unknown line → 404; **two lines where only the second is unknown → refused entirely**, no order row and no reservation. Partial acceptance is worse than refusal
- [X] T021 [US3] Run [quickstart](./quickstart.md) scenario 6: an inactive product is refused with **409**, distinguishable at a glance from scenario 5's 404
- [X] T022 [US3] **Negative control — Catalog unreachable.** `docker stop ecommerce-catalog`, submit an order. Confirm: zero orders created; **503**, not 500 and not 404; the message names the *lookup* rather than the product; and it gives up after the documented attempts rather than hanging. Paste it
- [X] T023 [US3] Run [quickstart](./quickstart.md) scenario 8 — inspect the request type and confirm neither field exists
- [X] T024 [US3] Add the fabricated-price scenario to `.github/scripts/verify-saga.sh`, sending raw JSON. Follow the existing conventions: `set -euo pipefail`, the probed Python interpreter, `fail()`/`pass()`, and `git update-index --chmod=+x` is **not** needed since the file already has its mode
- [X] T025 [US3] **Run the new scenario against `main` and confirm it FAILS.** The unfixed system is the control and it costs nothing today. Paste both directions: red on `main`, green on this branch. A check that has never been red proves nothing

**Checkpoint**: the defect cannot return quietly.

---

## Phase 6: CI and containers

- [X] T026 Confirm the `.proto` reaches the image — check `server/.dockerignore` does not exclude it, and that `docker build` produces working generated code for both Catalog and Order
- [X] T027 ~~Add Catalog's gRPC port to the `saga-e2e` job~~ **No job change needed** — services run natively there, so Catalog binds its default gRPC port 5157 and Order's default address is `http://localhost:5157`. But **`verify-auth.sh` did need changing**: it ordered product id `11111111-…-111111111111`, which has never existed in any catalogue, and that worked because Order believed whatever it was told. It now creates a real product first. That script's fictional order is further evidence for the defect, not a coincidence
- [X] T028 Open a pull request and confirm every job is green, including the new scenario — *Merged as #24; every check green (success, publish skipped on the PR). Ticked on 2026-09-27 from the PR's record.*

---

## Phase 7: Polish & Cross-Cutting

- [X] T029 Fill in SC-008: the before and after from T002. If the round trip is **not** invisible, say so — that is the evidence for the priced-cart shape in [#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19), not a reason to hide the number
- [X] T030 [P] Update `CLAUDE.md`: Catalog's second port in the service map, the first synchronous call and what it costs (Catalog down now stops orders), and that `OrderItemRequest` no longer carries a price — beside the existing note that it never carried a `UserId`
- [X] T031 [P] Update `docs/architecture/service-to-service-communication.md` — it currently describes this as a decision *about to be made*. Record what was chosen, and whether T001 changed it
- [X] T032 [P] Update `docs/README.md` if it lists ports or describes checkout
- [X] T033 Confirm `Order.Application` references no gRPC package: `grep -r "Grpc" server/src/Services/Order/Ecommerce.Order.Application/` must be empty. Principle II, checkable rather than intended

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


---

## What actually happened, against what this list assumed

- **T001 confirmed the plan instead of collapsing it**, which is the less interesting outcome and
  was still worth ten minutes. A minimal app with two plaintext endpoints, probed from a container:
  `Http1AndHttp2` refused h2c (`proto=0 code=000`) while a `Http2`-only control served it
  (`proto=2 code=200`). The second port is required, not preferred.

- **Calling `ListenAnyIP` at all REPLACES `ASPNETCORE_URLS`.** Configuring only the gRPC endpoint
  silently unbound REST: Catalog came up listening on 8081 alone, answered nothing on 8080 and went
  unhealthy. Kestrel says so — `Overriding address(es) 'http://+:8080'` — in a warning that is easy
  to scroll past while waiting for a container. Both endpoints are now declared together and the
  `app.Run(url)` fallback was removed, because it would be a third opinion about where to listen and
  the one that loses would lose silently. **T005 anticipated the `start-dev` path being forgotten;
  it did not anticipate that adding the second endpoint would delete the first.**

- **Adding a project means adding a line to the Dockerfile**, and nothing says so until the build
  fails. It copies each `.csproj` by name, restores, then publishes with `--no-restore`, so a
  missing line fails at publish with a message about the project rather than about the list. The
  file's own opening comment warns about seven near-identical things drifting; this list has the
  same shape and was not covered by that warning. A note has been added there.

- **T015's premise was wrong, and the truth is worse.** No test constructs `OrderItemRequest` —
  `grep -rln SubmitOrder tests/` returns nothing. **No test exercises order submission at all.** The
  fifteen Order tests are six query tests and nine settlement tests, building `OrderItem` entities
  directly. That is why removing two fields broke nothing and the solution built first time, and it
  is the real reason the defect survived: not badly-shaped tests, but no tests. `spec.md` and
  `research.md` were corrected rather than left to read plausibly.

- **Catalog cannot be edited.** `ProductsController` has `GetAll`, `GetById` and `Create` — no
  update, no deactivate, no delete. Scenarios 4 and 6 had to change the price, the name and
  `IsActive` directly in Catalog's database, which is legitimate here (what is under test is whether
  the **order** changes) and is a gap worth its own issue: a catalogue nothing can edit is not a
  catalogue.

- **The 503's message never reaches the customer.** `GlobalExceptionHandler` masks `detail` outside
  Development, by existing policy, so the carefully worded *"nothing was charged and no stock was
  reserved"* is invisible in a container. The **title** — `Service Unavailable` — does the essential
  work, which is telling a caller to retry rather than that the shop is broken. Changing the masking
  policy is a wider decision than this feature.

- **Three files were lost and rewritten, through my own mistake.** To run T025's control I reverted
  the submit path with `git checkout origin/main -- <files>` without committing first. That
  overwrites the index as well as the working tree, so the changes could not be restored afterwards
  and had to be retyped. The control itself was worth it and the correct sequence is to commit
  first — which is now what the branch does.

- **The control worked exactly as advertised, and it was free.** Run against `main`'s submit path,
  the new assertion failed with the defect named in full: *"Claimed 0.01 each and the order totals
  0.03, where the catalogue price of 9.99 x 3 is 29.97."* Against this branch it passes. Both
  directions, and after this merges the control costs a scratch branch.

- **The regression check is not a separate scenario.** `verify-saga.sh` now sends a fabricated price
  and a fabricated name on **every** run, as raw JSON with extra properties, and asserts the order
  comes back at the catalogue price under the catalogue name. A check that only runs when somebody
  remembers to run it is the kind that was missing here in the first place.
