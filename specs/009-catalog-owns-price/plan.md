# Implementation Plan: The Shop Decides What Things Cost

**Branch**: `009-catalog-owns-price` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-catalog-owns-price/spec.md`

## Summary

A customer can buy a 40,000,000 item for 1. The price and the product name on an order line come
from the customer's own request; nothing asks the catalogue. Reproduced on 2026-09-21 — the order
completed in two seconds, stock went 5 → 4 permanently, and the payment recorded `1.00 Approved`.

Order will obtain both from Catalog at submission and **freeze** them onto the order line, and
`OrderItemRequest` loses the two fields entirely. The mechanism is gRPC over h2c on a second port —
chosen to practise, and [research D2](./research.md) records that REST would have been cheaper for
the same guarantee.

This is the **first synchronous cross-service call in the system**. Everything today is messages,
verified: no `HttpClient` and no `IRequestClient` anywhere in `server/src`. The consequence is a
real reduction in availability — Catalog down now stops orders being placed, where today they were
placed at the wrong price — and that trade is the thing a reviewer should weigh hardest.

## Technical Context

**Language/Version**: .NET 10 / C# 13. `Grpc.AspNetCore` (server), `Grpc.Net.Client` +
`Grpc.Net.ClientFactory` (client), `Grpc.Tools` + `Google.Protobuf` (codegen).

**Storage**: No new table, no migration. Two existing columns change meaning — from a repetition of
the request to a record of what the shop charged.

**Testing**: The 61 existing tests plus a new scenario in `verify-saga.sh`. The negative control is
free while it lasts: **`main` itself accepts a fabricated price**, so the scenario can be run
against it and must fail there.

**Target Platform**: The containerised stack; Catalog gains host port 6057 / container 8081.

**Project Type**: Two services changed, one new class library, one shared building block touched.

**Performance Goals**: One extra round trip on checkout's critical path, on the same network.
Expected invisible; measured rather than assumed (SC-008).

**Constraints**:

- Protobuf has no decimal. Money is `decimal(18,2)` by constitution, so the price crosses the wire
  as a **string** and is parsed exactly ([contract](./contracts/catalog-pricing-grpc.md)).
- One plaintext port cannot serve HTTP/1.1 and HTTP/2 — telling them apart needs ALPN, which is part
  of TLS. Measured: the existing endpoint refuses h2c outright.
- `Ecommerce.Contracts` has **zero** package references and `CLAUDE.md` says so; the `.proto` cannot
  live there.
- `Ecommerce.Shared` has no exception that maps to 503, so a refusal caused by Catalog being down
  would answer 500 today.

**Scale/Scope**: One RPC, one new project, one new exception type, four documents. No migration.

## Constitution Check

*GATE: evaluated against [constitution v1.1.0](../../.specify/memory/constitution.md).*

| Principle | Verdict | How this design satisfies it |
| :-- | :-- | :-- |
| **I. Service Autonomy** | **PASS, and it is what the feature restores** | *"Exactly one service owns any given fact… the non-owning copy MUST NOT inform any decision — most importantly, a sell/no-sell, allow/deny, or **charge/refuse** decision."* Today the price informing the charge comes from the **client**, which owns nothing. Afterwards it comes from Catalog, which owns it. A synchronous call to Catalog's published interface is not reaching into its internals — the prohibition is on reading its database and calling its private parts, and this does neither. The design explicitly rejects giving Order its own price table ([D1](./research.md)), which is the option that *would* breach this. |
| **II. Clean Architecture Layering** | **PASS, and it constrains the design** | *Application depends on abstractions only — "never a transport package."* So the gRPC client cannot live in `Order.Application`. Application declares `ICatalogPrices` in `Common/Interfaces/`; `Order.Infrastructure` implements it and holds the channel; `Order.WebApi` registers it. Identical to `IPaymentGateway` / `StubPaymentGateway`. Application never references `Grpc.Net.Client`, and that is checkable by grep rather than by intention. |
| **III. Atomic Writes and Idempotent Messaging (NON-NEGOTIABLE)** | **PASS — and the ordering is the risk** | The handler's existing sequence is: stage the order, publish, `SaveChangesAsync` **once**. The lookup is inserted **before** all of it, so a failed lookup produces no row, no event and no reservation — all-or-nothing by construction. The trap to avoid is putting the call *between* the publish and the save, where a slow Catalog would widen the window between staging and commit. It goes first, before anything is staged. Idempotency is untouched: a repeated submission still resolves to the same guarded behaviour. |
| **IV. Identity Comes From the Token** | **PASS — and this feature is its argument extended** | `SubmitOrderCommand` still carries no `UserId`; nothing here changes how the caller is identified. The principle's *reasoning* — a field on a command that the client should never have supplied, and *"the mistake looks completely ordinary in review"* — is exactly what is being applied to `UnitPrice` and `ProductName`. FR-003 removes them rather than ignoring them, which is the same remedy `UserId` got. |
| **V. Evidence Over Assumption** | **PASS** | The defect was reproduced end to end before it was specified, not inferred from reading the handler. The transport decision rests on a measurement (`--http1.1` → 200, `--http2-prior-knowledge` → connection failed) rather than on documentation. Where something could not be established — whether `Http1AndHttp2` works on a plaintext endpoint in .NET 10 — the plan says to test it **first**, because the answer would change [D3](./research.md). |

**Development Workflow gates**:

- *"New behaviour that cannot be verified by hand requires an automated check."* FR-012, and it is a
  quickstart scenario run in **both** directions rather than an intention.
- *"A check that cannot express the property it is meant to guard MUST be replaced, not weakened."*
  The 61 existing tests cannot express this property — each supplies its own price and asserts
  against it. The new scenario sends raw JSON so it tests the server rather than the type system.

**No Complexity Tracking entries.** The gRPC choice is more expensive than REST and bends no
principle; it is recorded as a deliberate trade in [D2](./research.md), not waived as a violation.

**Post-Phase-1 re-check**: unchanged. Design added one RPC, one exception type and one project; no
new database, no new message, no new owner of any fact.

## Project Structure

### Documentation (this feature)

```text
specs/009-catalog-owns-price/
├── plan.md                              # this file
├── spec.md                              # what and why
├── research.md                          # 9 decisions + 3 things found while reading
├── data-model.md                        # where two fields come from, and the four answers
├── quickstart.md                        # 9 scenarios; the control is `main` itself
├── contracts/
│   └── catalog-pricing-grpc.md          # ports, proto, status codes, what it does not promise
├── checklists/requirements.md
└── tasks.md                             # produced by /speckit-tasks
```

### Source

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts.Grpc/            # NEW - .proto + codegen. NOT in Ecommerce.Contracts,
│   └── Protos/catalog_pricing.proto     #       which has zero package references today
└── Ecommerce.Shared/Exceptions/
    └── DependencyUnavailableException.cs # NEW - 503. Shared: all seven services see it

server/src/Services/Catalog/Ecommerce.Catalog.WebApi/
├── Grpc/CatalogPricingService.cs        # NEW - the server
└── Program.cs                           # second Kestrel endpoint, h2c, + gRPC health

server/src/Services/Order/
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/ICatalogPrices.cs          # NEW - abstraction only
│   └── Orders/Commands/SubmitOrder/
│       ├── SubmitOrderCommand.cs                    # OrderItemRequest loses 2 fields
│       ├── SubmitOrderCommandHandler.cs             # look up BEFORE staging anything
│       └── SubmitOrderCommandValidator.cs           # the price rules go with the fields
└── Ecommerce.Order.Infrastructure/
    └── Catalog/GrpcCatalogPrices.cs                 # NEW - holds the channel

server/docker-compose.app.yml            # publish 6057:8081
.github/scripts/verify-saga.sh           # the fabricated-price scenario
CLAUDE.md, docs/README.md                # the new port, the new call
```

**Structure Decision**: the client sits behind an Application-declared interface and lives in
Infrastructure, which Principle II requires and which also means swapping gRPC for REST later is one
file. The `.proto` gets its own building block so `Ecommerce.Contracts` keeps the property
`CLAUDE.md` advertises — a second, equally pure contracts project keeps the discipline; hiding
`Grpc.Tools` inside the existing one would not.

### The call, in the handler

```text
SubmitOrderCommandHandler
  1. ICatalogPrices.GetPrices(productIds)   ← ONE call, all ids, before anything is staged
  2. any line unresolved -> refuse          ← no row, no event, no reservation
  3. build order lines from what came back  ← frozen, not referenced
  4. stage order
  5. publish OrderSubmittedEvent
  6. SaveChangesAsync()                     ← exactly once, unchanged
```

Steps 4–6 are untouched. The lookup goes **first**, not between the publish and the save.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
