# Implementation Plan: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Branch**: `024-withdraw-a-product` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-delete-product/spec.md`

## Summary

Add `DELETE /api/products/{id}` to Catalog, Admin only. `DeleteProductCommand` loads the product with
its variants, collects every variant id, stages the product and its variants for removal, publishes
`ProductDeletedEvent(ProductId, VariantIds, DeletedAt)` and saves once - so the deletion and its
announcement share one transaction through Catalog's outbox. Inventory consumes the event
(`ProductDeletedConsumer` → `ForgetProductCommand`) and drops the `stock_items` rows for those variant
ids with one `ExecuteDeleteAsync` statement, which is naturally idempotent.

Plus `server/seed/clean-test-debris.py`, which lists the catalogue through the gateway, keeps every
product whose SKU `seed/cameras.json` names, and deletes the rest through the new endpoint - dry run
unless `--yes`.

One thing the design did not anticipate surfaced during verification: the Inventory consumer was
written and **not registered** in `Program.cs`, so it never ran and never complained. It was caught by
counting stock rows (125) against variants (23) after the cleanup had already run, and registered in
the same PR ([research.md D5](./research.md)).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (EF Core outbox in Catalog, consumer in Inventory),
MediatR 12.4.1, EF Core with Npgsql (`ExecuteDeleteAsync`), `Ecommerce.Contracts`, `Ecommerce.Shared`;
Python 3 standard library for the cleaner (`urllib`, `json`)

**Storage**: PostgreSQL 16 - `ecommerce_catalog_db` (5433) and `ecommerce_inventory_db` (5437). No
schema change ([data-model.md](./data-model.md))

**Testing**: xUnit against a real PostgreSQL in `Ecommerce.Catalog.Tests` (with MassTransit's test
harness to read what was published) and `Ecommerce.Inventory.Tests`; Bruno requests through the gateway

**Target Platform**: The existing Catalog (5057) and Inventory (5060) services behind the gateway (5000)

**Project Type**: Backend microservices, Clean Architecture

**Performance Goals**: None stated. Deletion is rare and administrative

**Constraints**: The deletion and its announcement commit together (constitution III); every variant id
must be announced, because stock is counted per variant and only the first variant reuses the product's
id (specs/020); orders must not change

**Scale/Scope**: A development catalogue of 14 cameras and, at the time, 97 debris products

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog deletes its own rows; Inventory drops its own rows on a message. Neither touches the other's database, and the cleaner goes through the gateway rather than SQL. The only shared code added is a pure record in `Ecommerce.Contracts/Catalog`. Orders are deliberately not told, because they own frozen copies of what was bought |
| **II. Clean Architecture Layering** | **Pass.** `DeleteProductCommand` and `ForgetProductCommand` live in Application under `Commands/<UseCase>/`, depending on `IProductRepository` / `IStockRepository` and `MassTransit.Abstractions`; EF Core stays in the repositories; the controller only dispatches; the consumer sits in WebApi |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The handler stages the removal, publishes, then calls `SaveChangesAsync` once. The consumer is idempotent by construction: a `DELETE ... WHERE "ProductId" IN (...)` on a row that is already gone affects zero rows, and `ForgetProductTests` asserts the second delivery returns 0 |
| **IV. Identity Comes From the Token** | **Pass.** `[Authorize(Roles = "Admin")]` on the action; the role comes from the validated token. The command carries only the product id |
| **V. Evidence Over Assumption** | **Pass, with a finding.** Both new test classes run against a real PostgreSQL, because the claims (cascade of options and prices, a freed unique SKU, rows dropped) belong to the database. Verification against the running stack is what found the unregistered consumer, which every unit test had passed without - the tests call the handler, not the bus. The PR says so rather than leaving the orphaned rows unmentioned |

**Post-design re-check**: no violations. One consequence is recorded rather than hidden: stock
reservations for a deleted variant are left in `stock_reservations`, because that table has no foreign
key to `stock_items` (see [data-model.md](./data-model.md)); they belong to a product nobody can order.

## Project Structure

### Documentation (this feature)

```text
specs/024-delete-product/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Six decisions with rejected alternatives
├── data-model.md        # No schema change; what is removed and in what order
├── quickstart.md        # Validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── contracts/
│   ├── http-api.md      # DELETE /api/products/{id}
│   └── messages.md      # ProductDeletedEvent
└── tasks.md             # Reconstructed task list, all done
```

### Source Code (repository root, as changed by #61)

```text
server/src/BuildingBlocks/Ecommerce.Contracts/Catalog/
└── ProductDeletedEvent.cs                                   # new

server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductRepository.cs              # + Remove(Product)
│   └── Products/Commands/DeleteProduct/DeleteProductCommand.cs   # new: command + handler
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs   # + Remove
└── Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs    # + DELETE {id:guid}, Admin

server/src/Services/Inventory/
├── Ecommerce.Inventory.Application/
│   ├── Common/Interfaces/IStockRepository.cs                # + ForgetAsync
│   └── Stock/Commands/ForgetProduct/ForgetProductCommand.cs # new: command + handler
├── Ecommerce.Inventory.Infrastructure/Persistence/Repositories/StockRepository.cs   # + ForgetAsync
└── Ecommerce.Inventory.WebApi/
    ├── Consumers/ProductDeletedConsumer.cs                  # new
    └── Program.cs                                           # + AddConsumer<ProductDeletedConsumer>()

server/tests/
├── Ecommerce.Catalog.Tests/DeleteProductTests.cs            # new, 4 tests
└── Ecommerce.Inventory.Tests/ForgetProductTests.cs          # new, 5 tests

server/seed/clean-test-debris.py                             # new
bruno/product/create a product to delete.yml                 # new
bruno/product/delete a product.yml                           # new
bruno/product/a deleted product is gone.yml                  # new
bruno/security-checks/customer cannot delete a product (403).yml   # new
CLAUDE.md                                                    # cleaner usage, test counts
```

**Structure Decision**: Follows the existing use-case folders. The Inventory command sits beside
`RegisterProductCommand` under `Stock/Commands/`, because it is that command's mirror. No gateway
change: `/api/products/{**catch-all}` already routes to Catalog.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **The product's image file stays on the volume.** Nothing in this PR deletes it; specs/029 found and
  closed that gap.
- **The stock rows left by the 97 deletions made before the consumer was registered** were not cleaned
  up. They are invisible - nothing references them - but they exist, and the PR says so.
- **Stock reservations** for deleted variants remain in `stock_reservations`.
- **Categories** full of debris remained (95 against two real ones); deleting an empty category arrived
  with #62, recorded in [specs/025](../025-storefront-redesign/) although its code comments say specs/024.
- The test scripts still created a product on every run; they began removing what they made in
  specs/073 (#118).

Since then: specs/027 opened deletion to a product's own seller (`Seller,Admin`, with the ownership
check), specs/029 made it delete the product's images, and specs/041 added an audit entry. The event
and the Inventory consumer are unchanged.
