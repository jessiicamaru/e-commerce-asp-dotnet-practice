# Implementation Plan: A product inserted without a review status waits for review

**Branch**: `093-review-default-pending` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #184

## Summary

One Catalog migration changes the database default of `products.ReviewStatus` from `'Approved'` to `'Pending'`, by SQL,
so an image from before specs/045 running after a rollback files a seller's new product in the moderators' queue
instead of on sale. The EF model deliberately declares no default (the enum's CLR default is `Approved`, and a model
default would make EF omit it from every INSERT). Two tests hold both halves.

## Technical Context

- `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926201531_ReviewStatusDefaultsToPending.cs`
  (+ `.Designer.cs`; the snapshot is unchanged)
- `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs` (a comment)
- `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`

**Language/Version**: C# 13 / .NET 10.0; PostgreSQL 16 DDL

**Primary Dependencies**: EF Core 10 migrations (`dotnet ef` 10.0.10 tools)

**Storage**: `ecommerce_catalog_db` (5433) - one column default

**Testing**: xUnit against a freshly created and migrated PostgreSQL database (`Ecommerce.Catalog.Tests`); mutations

**Target Platform**: Catalog; the migration runs at the service's startup in containers

**Performance Goals**: `SET DEFAULT` is a catalog-only change - no table rewrite, no lock beyond a brief `ACCESS
EXCLUSIVE` for the DDL

**Constraints**: no row rewritten; every image, earlier or later, keeps reading and writing the column

**Scale/Scope**: one migration, one comment, two tests

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own table; no other service reads the default. |
| **II. Clean Architecture Layering** | **Pass.** A migration and a mapping comment in Infrastructure; the rule (a seller's product starts `Pending`) stays in the Application layer, unchanged. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable** - no write path, message or handler changes. |
| **IV. Identity Comes From the Token** | **Not applicable** - no request handling changes. |
| **V. Evidence Over Assumption** | **Pass.** The new test failed before the migration; the stored status of the shop's product is now asserted from the database rather than the response; the migration mutation and the model-default mutation are each caught; `Ecommerce.Catalog.Tests` 217/217. |

The constitution's rollback rule ("a schema change must not strand an earlier image") is met: no column is dropped,
renamed or narrowed; the `schema-compatibility` job has nothing to flag.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/093-review-default-pending/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # The migration, the column, who writes what
├── quickstart.md
├── contracts/
│   └── none.md          # No contract change; what an old image's product looks like now
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As listed in Technical Context, plus `CLAUDE.md`, `docs/features/catalog.md`, `docs/project/backlog.md`,
`docs/project/timeline.md`. `docs/reference/` is not regenerated: `generate_reference.py` records no column defaults -
run on this branch, it changed only its commit stamps.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- During a rollback to a pre-045 image, the shop's own new products wait for review too (spec, Decision).
- Other columns' defaults were not audited against earlier images.
