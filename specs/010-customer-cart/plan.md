# Implementation Plan: Somewhere to Put What You Intend to Buy

**Branch**: `010-customer-cart` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

## Summary

A new **Cart** service — the eighth — holds what each signed-in customer intends to buy. Checkout
reads the caller's cart over gRPC, forwarding the customer's token so Cart identifies them the same
way every service does. The ordered lines leave the cart when the order **completes**, not when it is
submitted, so a declined payment leaves the cart intact. The cart stores **no price**: it asks
Catalog when read, and checkout charges Catalog's price at submission as feature 009 built.

The design turns on one finding: `OrderCompletedEvent` carries **only** an order id. So Cart consumes
three events and keeps its own record of each checkout, and — because nothing orders delivery across
message types, which is what #15 was — **whichever event arrives second applies the removal**.

## Technical Context

**Language/Version**: .NET 10 / C# 13, Clean Architecture like every other service.

**Primary Dependencies**: EF Core + Npgsql, MassTransit 8.3.6 (consumers, no publishing),
`Grpc.AspNetCore` (server), `Grpc.Net.ClientFactory` (Cart → Catalog, Order → Cart).

**Storage**: `ecommerce_cart_db`, host port **5439**. Three tables — [data-model](./data-model.md).

**Testing**: `Ecommerce.Cart.Tests` against a **real PostgreSQL**, for the checkout-outcome
transitions, because the guarantees are a row lock and a guarded flag. Plus `verify-saga.sh` and
`verify-auth.sh`, which must now fill a cart before checking out.

**Target Platform**: containerised stack; REST **5062**, gRPC **6062**.

**Constraints**:

- `OrderCompletedEvent` has no items ([D2](./research.md)).
- Events can arrive in either order ([D3](./research.md)).
- `ListenAnyIP` replaces `ASPNETCORE_URLS`; both endpoints declared together ([D6](./research.md)).
- A consumer's class name is its queue name; Cart's are prefixed `CartSvc` ([D7](./research.md)).
- Adding a project means adding its `COPY` line to the Dockerfile.

## Constitution Check

*GATE: evaluated against [constitution v1.1.0](../../.specify/memory/constitution.md).*

| Principle | Verdict | How |
| :-- | :-- | :-- |
| **I. Service Autonomy** | **PASS** | Cart owns its database exclusively. It holds **no copy of price**, so the rule that a non-owning copy may not inform a charge is satisfied by having nothing to misuse. It reads Catalog through Catalog's published interface, and Order reads Cart through Cart's. |
| **II. Clean Architecture** | **PASS** | Four projects, dependencies inward. `ICatalogProducts` and `ICartReader` are declared in the Application layers; the gRPC clients live in Infrastructure. |
| **III. Atomic Writes & Idempotent Messaging** | **PASS — and it drives the design** | Every consumer is idempotent by a **guarded state transition in the database** (`checkout_outcomes.Applied` under `FOR UPDATE`), not by configuration alone, as the principle requires. Cart publishes nothing, so there is no outbox write to order. |
| **IV. Identity from the Token** | **PASS — preserved across a hop** | No endpoint accepts a user id, REST or gRPC. Order forwards the customer's token to Cart rather than passing a user id, so Cart reads identity through `ICurrentUser` like everything else ([D4](./research.md)). |
| **V. Evidence over Assumption** | **PASS** | The completion event's missing items were read from the contract, not assumed. Out-of-order delivery is designed for because it has already happened here (#15). The negative controls run against a real database. |

**No Complexity Tracking entries.** A second synchronous dependency on checkout is a cost, recorded in
[D4](./research.md), not a violation.

## Project Structure

```text
server/src/Services/Cart/
├── Ecommerce.Cart.Domain/          Cart, CartLine, CheckoutOutcome
├── Ecommerce.Cart.Application/     commands/queries + ICartRepository, ICatalogProducts
├── Ecommerce.Cart.Infrastructure/  EF, migrations, GrpcCatalogProducts
└── Ecommerce.Cart.WebApi/          REST, CartReading gRPC, three consumers, two endpoints
server/tests/Ecommerce.Cart.Tests/  real PostgreSQL on 5439

server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/
├── catalog_pricing.proto           + DescribeProducts (additive)
└── cart_reading.proto              NEW

server/src/Services/Order/          SubmitOrderCommand reads the cart; ICartReader + token forwarding
server/src/Services/Catalog/        DescribeProducts
server/Dockerfile, docker-compose*.yml, Ecommerce.slnx, gateway routes, ci.yml, both scripts
```

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
