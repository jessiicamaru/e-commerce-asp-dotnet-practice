# Implementation Plan: A person downloads the data the shop holds about them

**Branch**: `111-my-data-export` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #217 (part 1)

## Summary

Six services each answer `GET .../my-data` with the caller's own rows, grouped by table, plus the tables they withhold
and why. Each declares a personal-data inventory, and a test holds that inventory to its EF model, so a new table cannot
be forgotten. The storefront composes one JSON download from the six answers.

## Technical Context

- **Shared**: `Ecommerce.Shared/PersonalData`, holding `MyDataResponse`, `WithheldTable` and `PersonalDataInventory`,
  plus a test helper that compares an inventory with a model.
- **Identity, Catalog, Order, Cart, Payment, Activity**: in each service,
  - `Application/MyData/`: the query, the handler and the inventory;
  - a repository read;
  - a controller action;
  - a `MyDataTests` class covering the inventory, the sections and the two-people case.
- **Storefront**: `services/account` gets `myData()` across the six, plus `utils/account/my-data.ts` to compose and
  download the file, the button on `/account`, and words.
- **Bruno**: six requests, plus six 401s in `security-checks/`.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql; TanStack Query

**Storage**: none new

**Testing**: xUnit against real PostgreSQL in each service; Vitest; Bruno

**Target Platform**: six services and the storefront

**Performance Goals**: one request per service; each reads a handful of indexed queries by the caller's id

**Constraints**:
- No secret and no other person's data in any export.
- Each service reads only its own database.

**Scale/Scope**: 6 routes, 6 inventories, 1 button

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service answers from its own database. The storefront composes, as for `/admin/overview`. No new edge between services. |
| **II. Clean Architecture Layering** | **Pass.** Queries and inventories live in Application, reads in Infrastructure, and the shapes in Shared. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** The feature only reads. |
| **IV. Identity Comes From the Token** | **Pass.** No route takes a person's id; each filters by `ICurrentUser.Id`. |
| **V. Evidence Over Assumption** | **Planned.** Inventory tests hold the declaration to each model, two-people tests run against PostgreSQL, plus mutations and Bruno. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/111-my-data-export/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/PersonalData/          (new)
server/src/Services/<Service>/…Application/MyData/                (new, six services)
server/tests/Ecommerce.<Service>.Tests/MyDataTests.cs              (new, six)
client/src/utils/account/my-data.ts, pages/account
bruno/auth/, bruno/security-checks/
```

## Complexity Tracking

No violation to justify. Six small, uniform endpoints are chosen over a gathering service because a gathering service
would need five new synchronous edges (research D1).
