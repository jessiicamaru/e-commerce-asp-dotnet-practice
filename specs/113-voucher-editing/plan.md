# Implementation Plan: A voucher's terms can be corrected

**Branch**: `113-voucher-editing` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #219

## Summary

`PUT /api/vouchers/{id}` corrects an active voucher's name, end date, limits and minimums - one guarded statement for
the limits, the amounts' and condition's rows beside it, and the audit entry, in one transaction. The shared voucher
page gets "Edit".

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core + Npgsql, MediatR, FluentValidation; TanStack Query, shadcn/ui
**Storage**: Order's PostgreSQL; no schema change
**Testing**: xUnit against PostgreSQL (with a race), Vitest, Bruno
**Target Platform**: the compose stack and CI
**Project Type**: microservice + web storefront
**Performance Goals**: none beyond one round trip per edit
**Constraints**: no order's price may change (specs/069's frozen redemptions)
**Scale/Scope**: one endpoint, one dialog

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order's own table; no call to any other service. |
| **II. Clean Architecture Layering** | **Pass.** Command and validator in Application, the guarded statement in the repository. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The edit and its audit entry commit together (the entry through the outbox, staged before the save). A repeated edit writes the same values. |
| **IV. Identity Comes From the Token** | **Pass.** The owner is the token's; no seller id in the request. |
| **V. Evidence Over Assumption** | **Planned.** Tests against PostgreSQL including a race of an edit and claims, mutations, Bruno. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/113-voucher-editing/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Application/Vouchers/ (EditVoucherCommand, validator, handler)
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/VoucherRepository.cs (TryEditAsync)
server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/VouchersController.cs (PUT {id})
server/tests/Ecommerce.Order.Tests/VoucherEditingTests.cs
client/src/components/voucher/ (voucher-edit dialog), services/voucher, hooks/voucher
bruno/seller/, bruno/security-checks/
```

## Complexity Tracking

No violation to justify.
