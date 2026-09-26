# Implementation Plan: Audit gaps and the misleading reuse warning

> Completed on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Branch**: `058-audit-gaps` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #128 (part A)

## Summary

Five writes that reached the database without an audit entry now record one, through the `IAuditTrail` every
service already has (specs/041), staged before the save of the change they describe. Separately, Identity's
refresh handler narrows what it calls reuse: only a **rotated** token presented again after the grace window
revokes every session and is recorded as `SessionReuseDetected`; a token revoked by a lock, a ban or an earlier
sweep is a stale tab and gets the same 401 with nothing else ended. No table, endpoint or message type changed.
Decisions and the rejected alternatives are in [research.md](research.md).

## Design

- **Catalog:**
  - `SetCategoryTranslationCommandHandler` and `RemoveCategoryTranslationCommandHandler` gain
    `IAuditTrail` and record before their one save.
  - `RemoveProductTranslationCommandHandler` records `ProductTranslationRemoved` before
    `AfterSellerEditAsync`.
- **Identity:**
  - `SetDefaultAddressCommandHandler` records inside its transaction, before the second save.
  - `LogoutCommandHandler` records `SignedOut` with `AuditActors.Of(user)`: sign-out may carry no access
    token, so the actor comes from the row, as it does for sign-in.
  - `RefreshTokenCommandHandler` treats a revoked token as reuse only when `ReplacedByToken` is set and
    the grace window has passed. It records `SessionReuseDetected` before revoking. *Correction (2026-09-27):*
    this line first said the entry is "saved with the revocation". The code at the merge saves the entry with
    its own `SaveChangesAsync` and only then runs `RevokeAllRefreshTokensAsync`, which is a separate
    `ExecuteUpdate` statement - the entry is saved just before the revocation, not in the same transaction.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: `Ecommerce.Shared.Audit` (`IAuditTrail`, `AuditCategory`), MassTransit 8.3.6 with the EF
Core transactional outbox (unchanged), MediatR 12.4.1

**Storage**: No schema change. Catalog (`ecommerce_catalog_db`, 5433) and Identity (`ecommerce_identity_db`, 5435)
stage `AuditEntryRecorded` in their existing outbox tables; Activity (`ecommerce_activity_db`, 5440) keeps it

**Testing**: xUnit against real PostgreSQL, MassTransit's test harness to read what was published
(`AuditTests` in Catalog and Identity, `RefreshTokenReuseTests` in Identity)

**Target Platform**: Catalog (5057) and Identity (5056), unchanged hosting

**Project Type**: Fix across two existing Clean Architecture services

**Performance Goals**: None; one extra outbox row per audited write

**Constraints**: Each entry commits with its change (Principle III); no address content and no token in any entry;
the 401 for a revoked token stays identical in every case

**Scale/Scope**: Five handlers and one branch of the refresh handler

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service records its own writes through its own outbox; Activity learns of them only through `AuditEntryRecorded`, an existing contract. No database is read across a boundary |
| **II. Clean Architecture Layering** | **Pass.** The handlers depend on `IAuditTrail` from `Ecommerce.Shared`; no Infrastructure type reaches the Application layer, and no controller changed |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, with one recorded exception.** Every write's entry is staged before the one save of that write (inside the transaction for the default address). The reuse entry is saved on its own just before the bulk revocation (see the correction under Design): a revocation is then never unexplained, and a failed revocation leaves an entry saying more than happened, which the next presentation of the token repeats. Activity's insert stays idempotent on the entry id |
| **IV. Identity Comes From the Token** | **Pass.** The default-address entry's actor is the caller from `ICurrentUser`. Sign-out and reuse carry no trustworthy access token, so the actor comes from the session's own row (`AuditActors.Of(user)`), never from the request |
| **V. Evidence Over Assumption** | **Pass.** Each entry has a test that failed first, against real PostgreSQL. The stale-tab test fails first on the session it ended. Three mutations were run and each turned a test red (PR #141) |

**Post-design re-check**: no violations. The reuse entry's separate save is a decision (research D4), not a
violation of Principle III: it describes a security event, not the write it precedes, and it is the record of the
event that must survive.

## Project Structure

### Documentation (this feature)

```text
specs/058-audit-gaps/
├── spec.md
├── plan.md                  # This file
├── research.md              # Six decisions
├── data-model.md            # No schema change; the columns the reuse rule reads
├── quickstart.md            # Validation scenarios
├── contracts/
│   ├── messages.md          # AuditEntryRecorded: the five new actions
│   └── http-api.md          # The endpoints whose writes are now recorded; the refresh 401
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #141)

```text
server/src/Services/Catalog/Ecommerce.Catalog.Application/
├── Categories/Translations/SetCategoryTranslationCommand.cs   # set + remove handlers record
└── Products/Translations/SetProductTranslationCommand.cs      # remove handler records
server/src/Services/Identity/Ecommerce.Identity.Application/
├── Addresses/Commands/SetDefaultAddress/SetDefaultAddressCommandHandler.cs
├── Auth/Commands/Logout/LogoutCommandHandler.cs
└── Auth/Commands/Refresh/RefreshTokenCommandHandler.cs         # reuse = rotated + past grace
server/tests/
├── Ecommerce.Catalog.Tests/AuditTests.cs
├── Ecommerce.Identity.Tests/AuditTests.cs
├── Ecommerce.Identity.Tests/IdentityTestFixture.cs             # AgeRevocationAsync
└── Ecommerce.Identity.Tests/RefreshTokenReuseTests.cs
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/audit-and-notifications.md`,
`docs/features/auth/security-best-practices.md`, `docs/overview/project-overview.md`,
`docs/project/timeline.md`, `docs/testing/testing-strategy.md`.

**Structure Decision**: No new file in the services. Each entry is recorded in the handler that makes the write,
where every other entry of specs/041 already is.

## Constitution check

- III (atomic writes): every entry is staged before the save of the change it describes. Pass.
- V (evidence): each entry has a test that fails first. The stale-tab test fails first on the session it
  ended. Pass.

(The two lines above are the check as first written; the table above extends it to all five principles.)

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Nobody is told** about a lock, a ban, a hidden review or a sweep-delivered parcel yet; that is part B,
  [specs/059](../059-missing-notices/).
- A lock still leaves the person's current **access token** working until it expires; only refresh is refused.
  [specs/065](../065-revoke-access-tokens/) closed that later.
- A routine refresh is deliberately not on the record.
