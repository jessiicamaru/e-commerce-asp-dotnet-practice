# Implementation Plan: Shop applications

> Completed on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Branch**: `044-shop-applications` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/044-shop-applications/spec.md`

## Summary

Opening a shop becomes an application that a moderator or an administrator decides. `register-seller`
stops granting `Seller`: it creates a customer and a pending row in a new Identity table,
`shop_applications`, and a signed-in customer can apply the same way. Staff approve or reject from a
queue. Approval does what registration used to do - the `Seller` role, the `seller_profiles` row,
`SellerRegisteredEvent` to Catalog - plus an audit entry and a notification, all inside the transaction
whose guarded `UPDATE ... WHERE "Status" = 'Pending'` decided it, so a second approval is a 409 that
wrote nothing. The storefront gains `/open-shop`, `/admin/shops` and a way to renew a session so the new
role arrives without signing out. Reasoning is in [research.md](./research.md).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (server); TypeScript, React 19 (client)

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, EF Core with Npgsql, MassTransit (EF outbox, already
configured in Identity since specs/027), `Ecommerce.Shared` (`IAuditTrail`, `INotifier`,
`StaffRoles`, exceptions), `Ecommerce.Contracts` (no change); client: TanStack Query, axios,
react-i18next, shadcn/ui

**Storage**: PostgreSQL, `ecommerce_identity_db` (host port 5435): one new table, `shop_applications`

**Testing**: xUnit against a real PostgreSQL with MassTransit's test harness
(`Ecommerce.Identity.Tests`); Vitest with Testing Library (client); the Bruno collection through the
gateway

**Target Platform**: Identity service (HTTP 5056) behind the YARP gateway (5000); the storefront

**Project Type**: Web service change (Clean Architecture service) plus storefront pages

**Performance Goals**: None stated. The queue is paged (default 12, at most 50) and indexed on
`(Status, CreatedAt)`

**Constraints**: One pending application per person under any interleaving (FR-001); a decision applied
exactly once, with everything it causes in its transaction (FR-002, constitution III); the applicant
only ever from the token (constitution IV)

**Scale/Scope**: Not recorded. Nothing in the design depends on volume beyond the paged, indexed queue

### What was built (the original plan, kept)

- **Identity domain.** `ShopApplication` is stored in `shop_applications`:
  - Fields: id, user, shop name, description, phone, status (string), decision reason, decided by,
    decided at, created.
  - A partial unique index on `UserId WHERE "Status" = 'Pending'`.
  - An index on (status, created) for the queue.
- **`register-seller`** now creates the account with Customer only, plus a pending application, in one
  save. It no longer creates a `SellerProfile` and no longer publishes `SellerRegisteredEvent`.
- **`ShopApplicationHandlers`**:
  - Apply: Customer only; the applicant comes from the token.
  - Mine: the caller's own applications.
  - Queue: Staff.
  - Approve and reject: Staff.
  - `ShopApplicationRules.EnsureMayApplyAsync` holds the apply rules (not already a seller, nothing
    waiting). **Correction**: the original plan said it "is shared, so the two ways of applying cannot
    drift". At the merge only the apply handler calls it; `register-seller` does not, and needs not,
    because the account it creates can be neither (research D5).
- **Deciding.** `IShopApplicationRepository.TryDecideAsync` runs a guarded
  `UPDATE ... WHERE "Status" = 'Pending'` in its own transaction, then calls the handler's `stage`.
  - For an approval, the stage adds the Seller role and the `SellerProfile`, publishes
    `SellerRegisteredEvent`, and records the audit entry and the notification.
  - For a rejection, the stage records the audit entry and the notification.
  - Only the winner of the guarded update runs its stage.
- **Shared**: `NotificationKind.ShopApproved` and `ShopRejected`.
- **Gateway**: `/api/shop-applications/**` → identity.
- **Client**:
  - `/open-shop` shows the form and the history. When the latest application is approved,
    "Go to my shop" renews the session first, through `refreshSession`, which is new on `AuthState`.
  - The user menu offers "Open a shop" to anybody who does not sell yet.
  - `/admin/shops` is the queue, with a tab per status.
  - A moderator's console now opens on `/admin/shops`.

## Research

Full decisions with rejected alternatives are in [research.md](./research.md). The three the original plan
recorded, kept as written:

- **D1 - An application, not a flag on the user.** A rejected application stays, with its reason, and the
  person can apply again. The history is what staff decide from.
- **D2 - The decision guard is the concurrency control.** The shop's key is the user id, so a second
  approval would already fail on the insert. It would fail with a 500, though, and only after
  publishing. The guarded update stops the second approval before anything else is written.
- **D3 - No backfill.** Today's shops have a profile and the Seller role. Nothing reads an application
  to decide what somebody may do, so existing shops need no rows.

research.md adds D4 (the partial unique index), D5 (`register-seller` keeps its address), D6 (who
decides), D7 (the role arrives at the next renewal), D8 (a rejection needs a reason), D9 (one response,
two views), D10 (audit and notification kinds) and D11 (queue order).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The original plan's verdicts
are kept in the first sentence of each row.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity owns accounts, roles and applications; Catalog learns of an approved shop through the event it already consumes. No new contract, no cross-service read; Catalog's `sellers` copy stays display data and decides nothing (specs/027) |
| **II. Clean Architecture Layering** | **Pass for the dependency direction; deviation in layout.** `IShopApplicationRepository` is declared in Application `Common/Interfaces/` and implemented in Infrastructure, registered in its `DependencyInjection.cs`; the controller only dispatches through MediatR; Application uses `IPublishEndpoint` from `MassTransit.Abstractions` only (8.3.6, reached through `Ecommerce.Shared`). The deviation: every command, query, validator and the one handler class live in one file, `ShopApplications/ShopApplicationFeatures.cs`, not in `<Aggregate>/Commands/<UseCase>/` folders. It follows the precedent of `Sellers/SellerCommands.cs` (specs/027) and `Users/UserAdministration.cs` (specs/043); the record gives no justification (see Complexity Tracking) |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The decision, the role, the shop, the event, the audit entry and the notification commit in one transaction. The guarded update makes a second decision a no-op. Apply stages the row and the audit entry and saves once; `register-seller` went from two saves to one, so the account, the application, the audit entry and the session commit together. One pending per person is a partial unique index, not a code check alone. No new consumer; the existing ones are idempotent |
| **IV. Identity Comes From the Token** | **Pass.** The applicant comes from the token. A decision is a staff action on an application id in the route. `ApplyForShopCommand` has no user id; `DecidedBy` is the caller's token subject. The feature also brings `register-seller` in line with the principle's last rule - elevated privilege is never granted by self-registration - which, for `Seller`, it had been |
| **V. Evidence Over Assumption** | **Pass, with two gaps named.** Integration tests cover the role, the event, the race and the rules. Mutation checks cover the guard and the form. Bruno covers the round trip. The tests run against a real PostgreSQL, because the guarantees are the database's. Gaps: `ShopApplicationTests` asserts the event's `SellerId` but compares the shop name to a literal on both sides, so the event's `ShopName` is proven only end to end by Bruno's `the shop name reaches the catalogue`; and no test races two applications, so the partial-index 409 path (`IsOnePendingViolation`) is exercised by nothing automated |

**Post-Phase 1 re-check**: no violation of a principle's substance. The one deviation is the file layout
under Principle II, recorded below. The design did not change as a result of the check.

## Project Structure

### Documentation (this feature)

```text
specs/044-shop-applications/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # D1-D11 with rejected alternatives
├── data-model.md        # shop_applications, states, what did not change
├── quickstart.md        # Validation scenarios and the recorded results
├── contracts/
│   ├── http-api.md      # /api/shop-applications and the changed register-seller
│   └── messages.md      # SellerRegisteredEvent at a new moment, audit and notices
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/ShopApplication.cs                        # new: entity + ShopApplicationStatus
├── Ecommerce.Identity.Application/
│   ├── Common/Interfaces/IShopApplicationRepository.cs                          # new: + ShopApplicationRow
│   ├── ShopApplications/ShopApplicationFeatures.cs                              # new: commands, queries, validators, rules, handlers
│   └── Auth/Commands/RegisterSeller/RegisterSellerCommand.cs                    # changed: customer + application
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/ShopApplicationConfiguration.cs                           # new
│   ├── Persistence/ApplicationDbContext.cs                                      # DbSet<ShopApplication>
│   ├── Persistence/Repositories/ShopApplicationRepository.cs                    # new: TryDecideAsync
│   ├── Migrations/20260923204529_AddShopApplications.cs                         # new
│   └── DependencyInjection.cs                                                   # registers the repository
└── Ecommerce.Identity.WebApi/Controllers/ShopApplicationsController.cs          # new

server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs             # ShopApproved, ShopRejected
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json                      # two routes

server/tests/Ecommerce.Identity.Tests/
├── ShopApplicationTests.cs                                                      # new, 6 tests
├── IdentityTestFixture.cs                                                       # ApprovedSellerAsync
├── SellerRolesTests.cs, AuditTests.cs                                           # sellers now go through approval

client/src/
├── services/shop-applications/{index.ts,types.ts}                               # new
├── hooks/shop-applications/index.ts                                             # new
├── pages/open-shop/{index.tsx,index.test.tsx}                                   # new
├── pages/admin-shops/{index.tsx,index.test.tsx}                                 # new
├── pages/admin-home/index.tsx                                                   # moderator lands on /admin/shops
├── layouts/admin-layout/{index.tsx,index.test.tsx}                              # "Shop applications" link
├── components/layout/user-menu/index.tsx                                        # "Open a shop"
├── context/auth/{index.tsx,types.ts}                                            # refreshSession
├── routes/index.tsx, constants/query-keys/index.ts
└── locales/{en,vi}/{admin,seller,notifications}.json

bruno/seller/            # 7 new requests, "register a seller" changed, the rest renumbered
bruno/security-checks/applying to sell without a token is 401.yml
```

Touched outside the service: `CLAUDE.md` (the marketplace paragraph). `local/seed-demo.py` is gitignored
and was changed to approve its demo sellers (from the pull request).

**Structure Decision**: Identity only on the server, because applications, roles and shops are all
Identity's facts. The Application-layer file layout follows the two most recent Identity features
rather than the per-use-case folders (Principle II deviation, below).

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| All shop-application commands, queries, validators and one multi-request handler in `ShopApplicationFeatures.cs`, not foldered by use case (Principle II) | Not recorded. The file follows `Sellers/SellerCommands.cs` (specs/027) and `Users/UserAdministration.cs` (specs/043) | Not recorded - the per-use-case layout is the constitution's and was not argued against in the record |

## What this feature does not finish

- **Email confirmation.** A shop is a public claim in an address's name, and nothing here checks the
  address is the applicant's. Closed by specs/063.
- **Self-decision.** Nothing stops a member of staff who also holds `Customer` from approving their own
  application; it is audited, not prevented. The record does not say whether this was considered.
- **Taking a shop away.** There is no un-approve and no suspension of a shop; staff can lock or ban the
  account (specs/043).
- **Existing shops have no application history** (D3), so the history is only complete for shops opened
  from this feature on.
- **The role arrives at the next renewal**, not at the approval. The storefront renews on "Go to my
  shop"; any other open session learns at its next refresh or sign-in.
