---
description: "Task list for A stopped account's access token stops working within seconds"
---

# Tasks: A stopped account's access token stops working within seconds

> Completed on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first (constitution Principle V): the rule, the hook, the consumer and every
publisher, then Bruno and an end-to-end ban across services.

## Format: `[ID] [P?] [Story] Description`

The first five tasks are the list as written on 2026-09-25, kept verbatim. T006 onwards break them down.

- [X] T001 [US1] [US2] Tests first. Cover:
  - the rule and pruning, in `Ecommerce.Identity.Tests/AccessTokenRevocationTests.cs`;
  - the consumer recording a revocation;
  - each of the six Identity actions publishing `AccessTokensRevoked`;
  - `OnTokenValidated` failing a revoked token and passing a later one.
- [X] T002 [US1] The contract, `RevokedAccessTokens`, the `OnTokenValidated` hook and the consumer with its registration extension.
- [X] T003 [US1] Identity publishes in all six places. Every service registers the consumer.
- [X] T004 End to end:
  - ban a customer holding a token, and see Cart, Order, Catalog and Identity refuse it within seconds;
  - change a password, and see the storefront session refresh.
- [X] T005 Mutation checks. Docs:
  - security, messages and reference;
  - timeline, backlog, decisions and counts;
  - CLAUDE.md.

---

## Phase 1: Tests first

- [X] T006 [P] [US1] [US2] `AccessTokenRevocationTests` in `server/tests/Ecommerce.Identity.Tests/AccessTokenRevocationTests.cs`: refused before, accepted after; the same second accepted; the latest wins in any order; forgotten after an hour (with a fixed clock); asked by `sub` and `iat`; the hook refuses through a real validation; the consumer records what Identity published
- [X] T007 [P] [US1] `AccessTokenRevocationPublishingTests` in the same file: a lock, a ban, a role revoked, a password changed, a password reset and a reused refresh token each publish; a grant publishes nothing

## Phase 2: Foundational - the contract and the shared list

- [X] T008 [US1] `AccessTokensRevoked(UserId, RevokedAt, Reason)` in `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/AccessTokensRevoked.cs`
- [X] T009 [US1] [US2] `RevokedAccessTokens` (latest instant per user, whole-second rule, one-hour prune), `AccessTokensRevokedConsumer` and `AddAccessTokenRevocations(service)` (temporary endpoint `<svc>-access-revoked-<id>`) in `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/RevokedAccessTokens.cs`; the `MassTransit` package in `Ecommerce.Shared.csproj`
- [X] T010 [US1] In `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/DependencyInjection.cs`, register `TimeProvider` and the `RevokedAccessTokens` singleton, and add `OnTokenValidated` that fails a revoked token

## Phase 3: User Story 1 - tokens issued before a stop are refused everywhere (P1)

- [X] T011 [US1] Publish `Locked`, `Banned` and `RoleRevoked` before the save in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- [X] T012 [P] [US1] Publish `PasswordReset` in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/PasswordReset/PasswordReset.cs` and `PasswordChanged` in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Account/Account.cs`, each inside its transaction
- [X] T013 [P] [US1] Publish `SessionReuseDetected` before the reuse entry's save in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Refresh/RefreshTokenCommandHandler.cs`
- [X] T014 [US1] [US2] Add `x.AddAccessTokenRevocations("<svc>")` to the `Program.cs` of Identity, Catalog, Cart, Order, Inventory, Payment and Activity (`server/src/Services/*/Ecommerce.*.WebApi/Program.cs`)

## Phase 4: Polish

- [X] T015 [P] Bruno: move the moderator revoke in `bruno/admin-insights/an administrator revokes moderator.yml` to the end of the last folder that uses the token, and add `bruno/admin-insights/the revoked moderator's token stops at once.yml` (401)
- [X] T016 Six mutations, each caught ([quickstart.md](quickstart.md)); the whole server suite 635/635 in 9 projects; Bruno 205/205 requests, 336/336 tests
- [X] T017 End to end with the seven services rebuilt: a ban refused by six services in 1758 ms; a password change carries on after one refresh; the temporary queues listed (output in [quickstart.md](quickstart.md))
- [X] T018 [P] Docs: security §4.10 and §5, `jwt-setup`, `moderation-and-staff` (rule 9, its limits, a stale "sign-in has no rate limit"), `db-design`; decision 49; counts, timeline, backlog, `CLAUDE.md`; `docs/tools/generate_reference.py` lists a Shared consumer under each service, and `docs/reference/messages.md` is regenerated
- [X] T019 Merge through PR #148 (squash, 2026-09-25), closing #112

## Dependencies

T006-T007 before T008-T014. T008 before T009 (the consumer consumes the contract); T009 before T010 and T014. The
publishers (T011-T013) depend only on T008. T015-T018 after; T019 last.

## Notes

- 19 tasks; the five original ones are the summary, T006-T018 their breakdown.
- No migration in any service.
