# Implementation Plan: Long staff lists can be searched and filtered

**Branch**: `feat/249-staff-list-filters` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #249

## Summary

Two server queries gain filters (vouchers: search, state; users: role, state); four staff pages gain the matching controls, counts where a tab can say how many, and Find an order words its failures.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19  
**Primary Dependencies**: EF Core, FluentValidation; TanStack Query  
**Storage**: Order's and Identity's PostgreSQL - no migration  
**Testing**: xUnit against PostgreSQL; Vitest; Bruno  
**Target Platform**: Order, Identity, the storefront  
**Project Type**: microservices + web client  
**Performance Goals**: filters in SQL on already-scoped queries  
**Constraints**: optional query parameters, additive  
**Scale/Scope**: two queries, four pages  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service filters its own rows. |
| **II. Clean Architecture Layering** | **Pass.** Validation in validators, filters in repositories, controls in pages. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Reads only. |
| **IV. Identity Comes From the Token** | **Pass.** A seller's vouchers stay scoped by the token; the filters narrow, never widen. |
| **V. Evidence Over Assumption** | **Planned.** Order and Identity tests against PostgreSQL, Vitest, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/133-staff-list-filters/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Application/Vouchers/VoucherFeatures.cs
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/VoucherRepository.cs
server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs
server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs
server/tests/Ecommerce.Order.Tests/VoucherFilterTests.cs
server/tests/Ecommerce.Identity.Tests/UserFilterTests.cs
client/src/components/voucher/voucher-page
client/src/pages/{admin-users,admin-categories,admin-order-search}
bruno/
```

## Complexity Tracking

No violation to justify.
