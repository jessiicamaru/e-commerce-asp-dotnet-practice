---
description: "Task list for Email confirmation"
---

# Tasks: Email confirmation

> Completed on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first on both sides (constitution Principle V), plus Bruno through Mailpit and an
end-to-end run.

## Format: `[ID] [P?] [Story] Description`

The first five tasks are the list as written on 2026-09-25, kept verbatim. T006 onwards break them down.

- [X] T001 [US1] [US2] [US3] [US4] Tests first in `server/tests/Ecommerce.Identity.Tests/EmailConfirmationTests.cs`. Cover:
  - registering (both kinds) queues one confirmation email, and the account is unconfirmed;
  - the link confirms once, and a used, expired or unknown token is 400;
  - two submissions at once confirm once;
  - resend replaces the link, sends at most once a minute, and an already confirmed account is 409;
  - sign-in and refresh report `EmailConfirmed`;
  - an unconfirmed customer cannot apply to sell (403 `EmailNotConfirmed`);
  - approving an unconfirmed applicant is 409, and staff see the flag;
  - audit entries hold no token, and the sent row is scrubbed.
- [X] T002 [US1] [US2] [US3] [US4] Identity:
  - `EmailConfirmedAt`, the token entity, configuration and migration (with the backfill);
  - `DataInitializer`, the repository and `EmailConfirmations`;
  - both registrations, `AuthResponse`, the shop application rules, the template;
  - the controller endpoints, and the gateway routes.
- [X] T003 [US1] [US2] [US3] Storefront tests first, then:
  - the service, the user type, the banner, the `/confirm-email` page;
  - the `/open-shop` guard, the admin shops flag, and the words.
- [X] T004 Bruno:
  - the seller folder confirms through Mailpit, after "approving an unconfirmed applicant is 409";
  - security checks.

  Then end to end through Mailpit. Update `local/seed-demo.py`, which is not committed, so the demo still seeds.
- [X] T005 Mutation checks. Docs:
  - `features/auth/*`, `features/email.md`, `features/marketplace.md`;
  - the reference, regenerated;
  - timeline, backlog, decisions and counts;
  - CLAUDE.md.

---

## Phase 1: Foundational

- [X] T006 Add `EmailConfirmedAt` and `EmailConfirmed` to `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/User.cs`; create `EmailConfirmationToken.cs` beside it
- [X] T007 `EmailConfirmationTokenConfiguration` in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/`, the set on `ApplicationDbContext`, and migration `20260925060135_AddEmailConfirmation` with the backfill `UPDATE users SET "EmailConfirmedAt" = "CreatedAt"`
- [X] T008 [P] Confirm the seeded administrator in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/DataInitializer.cs`
- [X] T009 [P] `EmailTemplate.EmailConfirmation` in `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`; its `vi`/`en` words, the `/confirm-email?token=` link and `ScrubbedOnceSent` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs`
- [X] T010 `IOutgoingEmailRepository.Stage` in `Email/EmailInterfaces.cs` and `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email/OutgoingEmailRepository.cs`
- [X] T011 `IEmailConfirmationRepository` and `EmailConfirmationRepository` (add, delete unused, `SentSinceAsync` under a row lock, guarded `TryClaimAsync` and `TryConfirmAsync`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/EmailConfirmationRepository.cs`; register it and `EmailConfirmations` in both `DependencyInjection.cs`

## Phase 2: Tests first

- [X] T012 [P] The 11 tests in `server/tests/Ecommerce.Identity.Tests/EmailConfirmationTests.cs`; adapt `AuditTests`, `EmailTests`, `ShopApplicationTests` and `SignInThrottleTests`, and add confirmation helpers to `IdentityTestFixture.cs`
- [X] T013 [P] `client/src/components/layout/confirm-email-banner/index.test.tsx` and `client/src/pages/confirm-email/index.test.tsx`; the new cases in `pages/open-shop` and `pages/admin-shops`; `emailConfirmed` in `client/src/test/render.tsx` and three existing tests

## Phase 3: User Story 1 - a new account confirms its address (P1)

- [X] T014 [US1] `EmailConfirmations.StageAsync` and `ConfirmEmailCommand` with its handler and validator in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/EmailConfirmation/EmailConfirmation.cs`
- [X] T015 [US1] Stage the link in `RegisterCommandHandler` and `RegisterSellerCommandHandler` (and a `Language` on both commands); `EmailConfirmed` on `AuthResponse`, filled by both registrations, `LoginCommandHandler` and `RefreshTokenCommandHandler`
- [X] T016 [US1] `POST confirm-email` (anonymous, 204) and `RequestLanguage()` for both registrations in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`
- [X] T017 [US1] The `auth-confirm-email-route` (`sign-in`) and `auth-resend-confirmation-route` (`email`) in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`
- [X] T018 [P] [US1] `emailConfirmed` in `client/src/services/auth/types.ts` and `client/src/context/auth/index.tsx`; `Auth.confirmEmail`; `client/src/pages/confirm-email/index.tsx` (sends once, renews a signed-in session) and its route

## Phase 4: User Story 2 - send it again (P1)

- [X] T019 [US2] `ResendConfirmationCommand` and its handler (409 when confirmed; one a minute under a row lock; delete unused, stage, save) in `EmailConfirmation.cs`; `POST resend-confirmation` (`[Authorize]`, 202) in `AuthController.cs`
- [X] T020 [P] [US2] `Auth.resendConfirmation`; `client/src/components/layout/confirm-email-banner/index.tsx` in `client/src/layouts/main-layout/index.tsx`, with the 429 wording of specs/062

## Phase 5: User Story 3 - what an unconfirmed account may not do (P1)

- [X] T021 [US3] In `server/src/Services/Identity/Ecommerce.Identity.Application/ShopApplications/ShopApplicationFeatures.cs`: `ForbiddenException` with `code: EmailNotConfirmed` on applying; 409 on approving a pending, unconfirmed applicant; `ApplicantEmailConfirmed` on the staff response
- [X] T022 [US3] Carry the applicant's confirmation in `ShopApplicationRow` (`IShopApplicationRepository.cs`, `ShopApplicationRepository.cs`)
- [X] T023 [P] [US3] `client/src/pages/open-shop/index.tsx` asks to confirm first; `client/src/pages/admin-shops/index.tsx` marks unconfirmed applicants; `client/src/services/shop-applications/types.ts`; words in `client/src/locales/{en,vi}/{auth,admin,seller}.json`

## Phase 6: User Story 4 - accounts from before this (P1)

- [X] T024 [US4] Verify the backfill on the development database: 32 of 32 accounts confirmed with `EmailConfirmedAt = CreatedAt`

## Phase 7: Polish

- [X] T025 [P] Bruno: `mailpitUrl` in `bruno/environments/local.yml`; `seller/approving before the address is confirmed is 409.yml` and `seller/the seller confirms their email with the link.yml`, with the later seller requests renumbered; `security-checks/confirming with a made-up token is 400.yml` and `security-checks/sending a confirmation link without a token is 401.yml`
- [X] T026 Eleven mutations, each caught ([quickstart.md](quickstart.md)); Identity 127/127, Gateway 12/12, client 323/323; Bruno 199/199 requests and 326/326 tests
- [X] T027 End to end through the gateway and Mailpit (output in [quickstart.md](quickstart.md) scenario 3)
- [X] T028 [P] Docs: security §4.8 and §5, `db-design`, `email.md`, `marketplace.md`; decision 47; counts, timeline, backlog, `CLAUDE.md`; `docs/reference/{api,data-model,gateway}.md` regenerated
- [X] T029 Merge through PR #146 (squash, 2026-09-25), closing #106

## Dependencies

T006-T011 before the handlers. T012-T013 before T014-T023. US2 and US3 depend on US1's staging and on `EmailConfirmed`.
US4 is the migration (T007) plus its check. T025-T028 after; T029 last.

## Notes

- 29 tasks; the five original ones are the summary, T006-T028 their breakdown.
