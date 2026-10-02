# Implementation Plan: Staff roles only in a back-office session

**Branch**: `feat/278-staff-roles-back-office` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #278

## Summary

Identity marks every session with the app it was made for, taken from `Origin`. Staff roles are written into a token
only for a verified back-office session. The auth response says whether the account is staff, so the storefront can
still link to the back office. The tools that act as staff sign in as the back office.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19
**Primary Dependencies**: EF Core, MediatR; ASP.NET Core (`IHttpContextAccessor`)
**Storage**: Identity's PostgreSQL - `refresh_tokens.Client`, nullable, expand-only
**Testing**: xUnit against PostgreSQL; Vitest; Bruno; the verify scripts; Playwright
**Target Platform**: Identity, the storefront, the tools
**Project Type**: microservices + web client
**Performance Goals**: none - one header read per sign-in
**Constraints**: a rolled-back image ignores the column, so its sessions are storefront sessions with staff roles as before
**Scale/Scope**: one column, one decision, six tools

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity alone decides. Every other service authorizes from the token's roles, unchanged. |
| **II. Clean Architecture Layering** | **Pass.** The rule is in the Application (`SessionRoles`, `SessionClients.FromOrigin`); reading the header is the WebApi's (`ISessionClient`). |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The client is written with the refresh token in the existing save, and carried in the existing guarded rotation. |
| **IV. Identity Comes From the Token** | **Pass, strengthened.** Which app a session belongs to comes from the browser's `Origin` and is stored with the session, never from the body. The storefront's `staffAccount` only draws. |
| **V. Evidence Over Assumption** | **Planned.** Tests against PostgreSQL for both apps, refresh and old sessions; the origin rule's own tests; Bruno, the smoke scripts and Playwright; mutations. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/138-staff-roles-back-office/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/RefreshToken.cs      Client
server/src/Services/Identity/Ecommerce.Identity.Application/Auth/TwoFactor/SessionRoles.cs
server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Common/{SessionClients,AuthResponse}.cs
server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/{Login,Refresh,Register,RegisterSeller}
server/src/Services/Identity/Ecommerce.Identity.WebApi/Auth/OriginSessionClient.cs
server/tests/Ecommerce.Identity.Tests/BackOfficeSessionTests.cs
client/packages/core/src/{context/auth,constants/account}
.github/scripts/verify-{auth,saga}.sh, server/seed/*.py, bruno/, client/e2e/support/api.ts
```

## Complexity Tracking

No violation to justify.
