# Implementation Plan: Staff see a deleted account as deleted

**Branch**: `fix/241-deleted-accounts` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #241

## Summary

Identity's users query filters `DeletedAt IS NULL` unless asked, its response carries `DeletedAt`, and the moderation commands' common target lookup refuses a deleted account; the storefront draws it as deleted and adds the filter.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19  
**Primary Dependencies**: EF Core, MediatR; TanStack Query  
**Storage**: Identity's PostgreSQL (no migration: `users.DeletedAt` exists since specs/112)  
**Testing**: xUnit against PostgreSQL; Vitest; Bruno  
**Target Platform**: Identity, the storefront  
**Project Type**: microservice + web client  
**Performance Goals**: n/a  
**Constraints**: additive response field and query parameter  
**Scale/Scope**: one query, one guard, one page  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity's own rows. |
| **II. Clean Architecture Layering** | **Pass.** Filter in the repository, rule in the Application handler, drawing in the page. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The refusal is before any write; nothing to make atomic. |
| **IV. Identity Comes From the Token** | **Pass.** Staff identity from the token as before. |
| **V. Evidence Over Assumption** | **Planned.** Identity tests against PostgreSQL, Vitest, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/123-deleted-accounts-staff/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs
server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs
server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/UsersController.cs
server/tests/Ecommerce.Identity.Tests/
client/src/pages/admin-users, services/accounts, hooks/accounts
bruno/users/
```

## Complexity Tracking

No violation to justify.
