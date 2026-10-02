# Implementation Plan: A one-time handoff from the storefront to the back office

**Branch**: `feat/279-back-office-handoff` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #279

## Summary

Identity issues a single-use, 30-second, hashed handoff code to a signed-in staff account, and redeems it for a
two-factor challenge. The storefront's "Management platform" carries the code in the URL fragment. The back office's
`/auth/callback` redeems it and opens the sign-in form at the code step.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19
**Primary Dependencies**: EF Core, MediatR; YARP rate limiting
**Storage**: Identity's PostgreSQL - `back_office_handoffs` (new table)
**Testing**: xUnit against PostgreSQL; Vitest; Bruno; Playwright
**Target Platform**: Identity, the gateway (route + limit), both apps
**Project Type**: microservices + web client
**Performance Goals**: none
**Constraints**: never a session without the second factor; the code never in a request a server logs
**Scale/Scope**: two endpoints, one table, one page, one link

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity alone issues and redeems. The apps only carry the code. |
| **II. Clean Architecture Layering** | **Pass.** Handlers and their validators in the Application, the guarded claim in the repository, the endpoints in the controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Issuing writes the row and its audit entry in one save. Redeeming is one guarded statement, and its challenge is saved after it. |
| **IV. Identity Comes From the Token** | **Pass.** Issuing reads the user from the token, never the body. Redeeming yields only a challenge; the session still needs the code and the back office's `Origin`. |
| **V. Evidence Over Assumption** | **Planned.** Tests against PostgreSQL (once, expiry, made-up, who may, concurrency), Vitest for both pages, Bruno, Playwright, mutations. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/140-back-office-handoff/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Identity/.../Domain/Entities/BackOfficeHandoff.cs
server/src/Services/Identity/.../Application/Auth/Handoff/HandoffFeatures.cs
server/src/Services/Identity/.../Infrastructure/{Configurations,Persistence/Repositories}, Migrations/AddBackOfficeHandoffs
server/src/Services/Identity/.../WebApi/Controllers/AuthController.cs
server/src/ApiGateway/.../appsettings.json            the redeem route under the sign-in limit
server/tests/Ecommerce.Identity.Tests/HandoffTests.cs
client/packages/core/src/{services/auth,utils/back-office}
client/apps/storefront (the link), client/apps/back-office/src/pages/auth-callback
bruno/, client/e2e/
```

## Complexity Tracking

No violation to justify.
