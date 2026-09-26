# Implementation Plan: A stopped account's access token stops working within seconds

> Completed on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md).

**Branch**: `065-revoke-access-tokens` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #112

## Summary

Identity publishes one new event, `AccessTokensRevoked`, through its outbox with each of six changes. Every
service that validates tokens keeps an in-memory list of the latest revocation per user, fed by a shared consumer
on a temporary queue per instance, and the `OnTokenValidated` hook that `AddJwtAuthentication` installs refuses
any token of that user issued before the revocation's whole second. No table changed. Decisions below and in
[research.md](research.md) (decision 49 in [docs/project/decisions.md](../../docs/project/decisions.md)).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer` (`JwtBearerEvents.OnTokenValidated`),
MassTransit 8.3.6 - `Ecommerce.Shared` gains a `MassTransit` package reference for `IBusRegistrationConfigurator`

**Storage**: None. A `ConcurrentDictionary<Guid, DateTime>` per service instance; the event travels through
Identity's existing outbox

**Testing**: `AccessTokenRevocationTests` (the rule, the hook, the consumer) and `AccessTokenRevocationPublishingTests`
(the six publishers and a grant) in `Ecommerce.Identity.Tests`; Bruno; an end-to-end ban across services

**Target Platform**: Identity, Catalog, Cart, Order, Inventory, Payment, Activity

**Project Type**: Security fix across Shared, Contracts and seven services

**Performance Goals**: A revocation effective within seconds (1758 ms measured end to end); one dictionary lookup
per validated token

**Constraints**: fails open to the old 15 minutes, never closed; every instance hears every revocation; the session a
password change keeps must not refuse its own new token

**Scale/Scope**: One contract, one shared class, one line per service, six publishers

## Design

- **The message:** `Contracts/Identity/AccessTokensRevoked.cs`, `(Guid UserId, DateTime RevokedAt,
  string Reason)`.
- **`Ecommerce.Shared/Authentication/RevokedAccessTokens`** is a singleton holding
  `ConcurrentDictionary<Guid, DateTime>`:
  - `Revoke(userId, at)` keeps the latest instant per user, and prunes entries older than the retention;
  - `IsRevoked(userId, issuedAt)` is true when `issuedAt < floor(RevokedAt, seconds)`.
- **`AddJwtAuthentication`** registers the singleton and adds `JwtBearerEvents.OnTokenValidated`. It reads
  `sub` and `iat` from the principal, and calls `Fail` when the token is revoked, which answers 401.
- **`AccessTokensRevokedConsumer`** (Shared) records the revocation. Each service registers it with
  `x.AddAccessTokenRevocations("catalog")`: an endpoint `catalog-access-revoked-{instance}`, **temporary**,
  so every instance of a service gets every message.
- **Identity publishes** through `IPublishEndpoint` before the save that makes the change:
  - lock, ban and role revoke (`UserAdministration`);
  - password reset (`PasswordResetHandlers`);
  - password change (`AccountHandlers`);
  - reuse detected (`RefreshTokenCommandHandler`).

## Decisions

1. **A revocation list, not shorter tokens.** Five-minute tokens triple the refresh traffic and still
   leave five minutes. The list closes the gap in seconds, and costs one small dictionary per service.
2. **The event means "tokens issued before now", not "this account is stopped".** One rule then covers
   a ban, where the refresh fails and the person is signed out, and a password change or role revoke, where
   the refresh succeeds with the new state.
3. **In memory, per instance, temporary queue.** A restart forgets, and the worst case is the old
   behaviour. A durable per-service queue would deliver each message to one instance only.

## Constitution check

- **I. Service Autonomy.** No synchronous call: each service keeps its own read model of revocations, fed
  by events. That is the codebase's usual answer, acceptable here because a stale copy fails **open to
  the old 15-minute behaviour**, never closed. Pass.
- **III.** Published through Identity's outbox with the change. Pass.
- **V.** Unit tests of the rule, a consumer test, publish tests for all six actions, mutation checks, and
  an end-to-end ban across services. Pass.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The list above is the check as first
written (it named I, III and V); this table covers all five.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No synchronous call: each service keeps its own read model of revocations, fed by events. That is the codebase's usual answer, acceptable here because a stale copy fails **open to the old 15-minute behaviour**, never closed - unlike specs/031, where authorization had to be asked live because a stale copy would refuse wrongly |
| **II. Clean Architecture Layering** | **Pass.** The rule, the consumer and its registration live in `Ecommerce.Shared/Authentication`, cross-cutting infrastructure like `AddJwtAuthentication`; Identity's Application layer publishes through `IPublishEndpoint` (abstractions). The consumer is registered from each WebApi `Program.cs`. `Ecommerce.Shared` taking the `MassTransit` package is noted, not a layer violation: Shared is not an Application layer |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Published through Identity's outbox before the save of the change in all six places. The consumer is naturally idempotent: recording the same revocation twice leaves the latest instant, and an older one arriving late cannot overwrite a newer |
| **IV. Identity Comes From the Token** | **Pass.** The check reads `sub` and `iat` from the validated principal; nothing from the request. It strengthens the principle: a token now stops speaking for its user when Identity says so |
| **V. Evidence Over Assumption** | **Pass.** Unit tests of the rule, a hook test, a consumer test, publish tests for all six actions plus a grant, six mutation checks, and an end-to-end ban across services with the timing recorded (PR #148). Bruno's first run found the change working on its own moderator |

**Post-design re-check**: no violations. No schema change.

## Project Structure

### Documentation (this feature)

```text
specs/065-revoke-access-tokens/
├── spec.md
├── plan.md                  # This file
├── research.md              # Five decisions
├── data-model.md            # No table; the in-memory list
├── quickstart.md
├── contracts/
│   ├── messages.md          # AccessTokensRevoked
│   └── http-api.md          # the 401 a revoked token now gets
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #148)

```text
server/src/BuildingBlocks/Ecommerce.Contracts/Identity/AccessTokensRevoked.cs
server/src/BuildingBlocks/Ecommerce.Shared/
├── Authentication/RevokedAccessTokens.cs     # the list, the consumer, AddAccessTokenRevocations
├── Authentication/DependencyInjection.cs     # OnTokenValidated; the singleton
└── Ecommerce.Shared.csproj                   # MassTransit package
server/src/Services/Identity/Ecommerce.Identity.Application/
├── Users/UserAdministration.cs               # lock, ban, role revoked
├── Auth/Commands/PasswordReset/PasswordReset.cs
├── Auth/Commands/Account/Account.cs          # password change
└── Auth/Commands/Refresh/RefreshTokenCommandHandler.cs   # reuse
server/src/Services/{Identity,Catalog,Cart,Order,Inventory,Payment,Activity}/Ecommerce.*.WebApi/Program.cs
server/tests/Ecommerce.Identity.Tests/AccessTokenRevocationTests.cs
bruno/admin-insights/{an administrator revokes moderator,the revoked moderator's token stops at once}.yml
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/auth/{db-design,jwt-setup,security-best-practices}.md`,
`docs/features/moderation-and-staff.md`, `docs/overview/project-overview.md`, `docs/project/{backlog,decisions,timeline}.md`,
`docs/reference/messages.md` (regenerated), `docs/testing/testing-strategy.md`, and `docs/tools/generate_reference.py`
(lists a Shared consumer under every service that registers it).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **A service restarted within the hour** forgets its revocations; a token revoked before the restart then lives
  out its 15 minutes on that service. Recorded as the one remaining limit.
- **A role granted** still reaches a session at its next refresh; nothing pushes it.
- **A new service** that validates tokens must add `x.AddAccessTokenRevocations("<svc>")`, or it keeps the old
  window; nothing checks that it did.
