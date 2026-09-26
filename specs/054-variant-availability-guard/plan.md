# Implementation Plan: Variant availability guard

> Completed on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Branch**: `054-variant-availability-guard` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #124

## Summary

Delete one clause from the guarded `UPDATE` that records a variant's availability, so it compares observation
times only - the rule the product-level guard and the interface's own comment already state. One test for the
exact reordering, written first.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core `ExecuteUpdateAsync` (Npgsql), MassTransit (the existing
`StockAvailabilityChangedConsumer`), MediatR

**Storage**: PostgreSQL, `ecommerce_catalog_db` (5433) - `product_variants`, `products`; no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, `AvailabilityTests`)

**Target Platform**: Catalog service (5057)

**Constraints**: duplicates and overtaken announcements must still change zero rows (Principle III)

**Scale/Scope**: one `Where` clause

## Design

Remove `&& (v.Availability != isAvailable || v.AvailabilityObservedAt == null)` from
`ProductRepository.TryRecordVariantAvailabilityAsync`. The remaining guard is
`AvailabilityObservedAt == null || AvailabilityObservedAt < observedAt`, the same as the product-level
`TryRecordAvailabilityAsync`.

**Cost.** A same-value newer announcement now writes one row and recomputes one product's rollup, where
before it wrote nothing. Inventory announces only when availability may have changed, so repeats are
rare. Correctness is worth one statement.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- III (idempotent messaging): a duplicate still changes zero rows, and so does an overtaken announcement.
  Pass.
- V (evidence): the ordering test fails before the fix, and a mutation check re-adds the clause. Pass.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Inventory still owns stock; Catalog's availability is a display copy that nothing sells against (checkout reserves in Inventory under `FOR UPDATE`). The fix makes the copy follow the owner's latest word |
| **II. Clean Architecture Layering** | **Pass.** The change is inside the Infrastructure repository's query; the Application handler and the consumer are unchanged |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The write stays a single guarded `UPDATE`; a duplicate (same time) and an overtaken announcement (older time) affect zero rows; the rollup is recomputed only when a row changed, and is itself idempotent |
| **IV. Identity Comes From the Token** | **Pass, not engaged.** No endpoint |
| **V. Evidence Over Assumption** | **Pass.** The new test runs the exact reordering against a real PostgreSQL and failed before the fix; the mutation check is the original clause, which is that red run |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/054-variant-availability-guard/
├── spec.md
├── plan.md            # This file
├── research.md        # D1, D2
├── data-model.md      # The guarded UPDATE, before and after
├── quickstart.md
├── contracts/
│   └── messages.md    # StockAvailabilityChangedEvent, unchanged
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- `server/tests/Ecommerce.Catalog.Tests/AvailabilityTests.cs`
- `CLAUDE.md`, `docs/features/catalog.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`,
  `docs/project/backlog.md`, `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

Nothing is left open by it.
