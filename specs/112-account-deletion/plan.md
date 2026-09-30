# Implementation Plan: A person deletes their account

**Branch**: `112-account-deletion` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #217 (part 2)

## Summary

`DELETE /api/auth/me` with the password. Identity refuses staff, asks Order live over gRPC what is still open, then in
one transaction empties its user row, deletes everything else it holds about the person, and publishes
`AccountDeleted` and `AccessTokensRevoked`. Catalog, Order, Cart and Activity erase or anonymise their own rows on
`AccountDeleted`, as each service's specs/111 inventory now declares (`Kept` sections survive, every other section is
erased). The storefront's account page gets "Delete my account".

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core + Npgsql, MassTransit (outbox), Grpc.AspNetCore (Order's first gRPC server), MediatR
**Storage**: PostgreSQL per service; one new column, `users.DeletedAt`
**Testing**: xUnit against real PostgreSQL; MassTransit test harness for the consumers; Vitest; Bruno
**Target Platform**: the compose stack and CI
**Project Type**: microservices + web storefront
**Performance Goals**: none beyond a deletion answering within a second
**Constraints**: every write atomic with its message (Principle III); identity from the token (IV)
**Scale/Scope**: one endpoint, one gRPC call, one message, four consumers, one page section

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass, with one new synchronous edge.** Identity → Order (`AccountStanding`), asked live because it is a permission (research D2, as specs/031). Each service erases its own rows from one message; nobody writes another's database. Order unreachable means an account cannot be deleted - accepted: the check is the point. |
| **II. Clean Architecture Layering** | **Pass.** Commands and consumers' logic in Application, erasures in Infrastructure repositories, the gRPC client behind an Application interface (`IAccountStanding`), the message in Contracts. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Identity's emptying, deletions, audit entry and both messages commit in one `SaveChangesAsync` (publish before save). Each consumer's erasure is one transaction and idempotent by nature (fixed values, deletes by id). |
| **IV. Identity Comes From the Token** | **Pass.** No id in the request; Order's standing is answered from the forwarded token with an empty request message. |
| **V. Evidence Over Assumption** | **Planned.** Tests per service against PostgreSQL (the export after erasure is the evidence), a test per blocker, mutations, Bruno against containers, the storefront's page test. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): unchanged. I - the one new edge is as designed, asked before the
transaction, 503 when Order is down; each service erases its own rows. II - the gRPC client sits behind
`IAccountStanding` in Identity's Application layer, erasures behind `IAccountErasure` in each. III - Identity's staging
and save are one transaction; every consumer joins MassTransit's transaction when one is open, else opens its own under
the retry strategy, and every erasure is idempotent (a redelivery is tested in each service). IV - no id in the request,
the standing from the forwarded token. V - see the evidence in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/112-account-deletion/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Contracts/Identity/AccountDeleted.cs            (new)
server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/account_standing.proto      (new)
server/src/BuildingBlocks/Ecommerce.Shared/PersonalData/ (Kept), Exceptions/ConflictException (facts)
server/src/Services/Identity/…Application/Auth/Commands/DeleteAccount/                 (new)
server/src/Services/Identity/…Infrastructure/ (erasure, Order gRPC client, migration)
server/src/Services/Order/…WebApi/Grpc/AccountStandingService.cs + second Kestrel port (new)
server/src/Services/{Catalog,Order,Cart,Activity}/…/EraseAccountFrom*Consumer.cs      (new)
server/tests/*/AccountDeletionTests.cs
server/src/ApiGateway/…/appsettings.json (auth-me-delete-route)
server/docker-compose.app.yml, start-dev.*, .github/workflows/ci.yml (Order's gRPC port)
client/src/pages/account/delete-account.tsx
bruno/my-data/ (the deletion requests after the export ones), bruno/security-checks/
```

## Complexity Tracking

| Addition | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| Identity → Order gRPC, and Order's first gRPC port | A deletion must be refused while business is open, and that is a permission asked now | A message round trip needs a pending state and cannot answer the person now; the storefront asking first enforces nothing (research D2) |
