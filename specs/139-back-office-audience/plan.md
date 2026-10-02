# Implementation Plan: A separate token audience for the back office

**Branch**: `feat/280-back-office-audience` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #280

## Summary

Every service accepts two audiences and removes staff roles from any token not issued for the back office. Identity
issues back-office sessions' tokens for that audience. The change is in `Ecommerce.Shared.Authentication`, so the nine
services get it without a line of their own.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Microsoft.AspNetCore.Authentication.JwtBearer
**Storage**: none
**Testing**: xUnit through a real pipeline (TestServer), Identity's token tests, Bruno, the verify scripts, Playwright
**Target Platform**: all nine services (shared), Identity (issuing)
**Project Type**: microservices
**Performance Goals**: one claim filter per validated token
**Constraints**: nothing a storefront token may do changes; forwarded tokens keep working
**Scale/Scope**: one shared method, one setting, one line in the token generator

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service decides for itself from the token it receives; nothing is asked of Identity at run time. |
| **II. Clean Architecture Layering** | **Pass.** In the shared authentication building block, where validation already lives. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No writes. |
| **IV. Identity Comes From the Token** | **Pass, strengthened.** What a token may do now depends on whom it was issued for, checked by every service. |
| **V. Evidence Over Assumption** | **Planned.** Pipeline tests for both audiences and neither, Identity's issued audience, mutations, and the full tool suite. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/139-back-office-audience/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Authentication/{JwtSettings,DependencyInjection,StaffRoles}.cs
server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Security/JwtTokenGenerator.cs
server/tests/Ecommerce.Identity.Tests/BackOfficeAudienceTests.cs
```

## Complexity Tracking

No violation to justify.
