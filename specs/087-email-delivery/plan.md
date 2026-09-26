# Implementation Plan: Email delivery

> Written on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Branch**: `087-email-delivery` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/087-email-delivery/spec.md`

## Summary

Give administrators a read of `outgoing_emails` and one repair. In Identity, `EmailDelivery.cs` holds the list query
(`GetOutgoingEmailsQuery`, validated), the retry command, `MayRetry`, and a response record that has no field for
the data. `OutgoingEmailRepository` gains a paged read left-joined to `users` for the address, and `TryRetryAsync`,
one guarded `ExecuteUpdateAsync` from `Failed` to `Pending`. The retry runs inside the unit of work's transaction
with its audit entry. `EmailsController` (`Admin`) exposes both; the gateway routes `/api/emails`; the storefront
adds `/admin/email-delivery`. No migration.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript (React 19, TanStack Query) in the storefront

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, EF Core with Npgsql, `Ecommerce.Shared/Audit`;
shadcn/ui in the client

**Storage**: PostgreSQL 16, `ecommerce_identity_db` (5435): `outgoing_emails` read and updated, `users` read; no
schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Identity.Tests/EmailDeliveryTests`, 4); Vitest; Bruno

**Target Platform**: Identity (5056), gateway (5000), the storefront's admin console

**Project Type**: A new use case in one service, a gateway route, a client page

**Performance Goals**: None stated. The search is a `LIKE '%...%'` on `lower("Email")`, unindexed for a contains

**Constraints**: Never return the data; a retry must move an email at most once; never resend an expiring link

**Scale/Scope**: One table's rows, paged 1-50

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Emails and addresses are both Identity's own; no other service is read |
| **II. Clean Architecture Layering** | **Pass.** Query, command, validator and `MayRetry` in Application (`Email/EmailDelivery.cs`); the three repository methods declared in `EmailInterfaces.cs` and implemented in Infrastructure; the controller only dispatches |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The retry is one guarded `UPDATE ... WHERE "Status" = 'Failed'` inside a transaction that also stages the audit entry through the outbox and saves once, so the entry exists only if the email moved, and a repeat affects zero rows (409) |
| **IV. Identity Comes From the Token** | **Pass.** `[Authorize(Roles = "Admin")]` on the controller; the audit's actor is `ICurrentUser`; no identity in the request |
| **V. Evidence Over Assumption** | **Pass.** The tests run against a real PostgreSQL and read what was actually sent; three mutations; Bruno against the rebuilt identity, gateway and storefront, including the 403 and 401 |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/087-email-delivery/
├── spec.md
├── plan.md              # This file
├── research.md          # Five decisions
├── data-model.md        # outgoing_emails states and the retry
├── quickstart.md
├── contracts/
│   └── http-api.md      # GET /api/emails, POST /api/emails/{id}/retry
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Identity/
├── Ecommerce.Identity.Application/Email/EmailDelivery.cs          # new
├── Ecommerce.Identity.Application/Email/EmailInterfaces.cs        # PageAsync, GetWithRecipientAsync, TryRetryAsync
├── Ecommerce.Identity.Infrastructure/Email/OutgoingEmailRepository.cs
└── Ecommerce.Identity.WebApi/Controllers/EmailsController.cs      # new
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json         # emails-route, emails-root-route
server/tests/Ecommerce.Identity.Tests/EmailDeliveryTests.cs         # new, 4 tests
client/src/services/outgoing-email/{index.ts,types.ts,index.test.ts}
client/src/hooks/outgoing-email/index.ts
client/src/pages/admin-email-delivery/{index.tsx,index.test.tsx}
client/src/routes/index.tsx, client/src/layouts/admin-layout/index.tsx, client/src/constants/query-keys/index.ts
client/src/locales/{en,vi}/admin.json
bruno/admin-users/{a moderator cannot read the email log is 403, an administrator reads the sent emails,
                   an email that did not fail is not sent again}.yml        # seq 31-33
bruno/security-checks/the email log without a token is 401.yml              # seq 60
```

Docs in the same change: `CLAUDE.md`, `docs/features/email.md`, `docs/project/decisions.md` (row 68),
`docs/reference/api.md`, `docs/reference/gateway.md`, `docs/overview/project-overview.md`,
`docs/testing/testing-strategy.md`, `docs/project/timeline.md`, `docs/project/backlog.md`.

**Structure Decision**: one file per use case family, as the email templates of specs/077 are; the service folder
follows the client conventions (`services/outgoing-email` with its types beside it, because the class and the model
cannot share a name).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

Nobody is alerted when an email fails; an administrator sees it by opening the page.
