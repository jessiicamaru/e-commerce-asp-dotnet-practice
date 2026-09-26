---
description: "Task list for Limits on the sign-in and email endpoints"
---

# Tasks: Limits on the sign-in and email endpoints

> Completed on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first: a new gateway test project through the real pipeline, Identity tests against
real PostgreSQL, storefront tests, a Bruno check and an end-to-end run (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

The first seven tasks are the list as written on 2026-09-25, kept verbatim. T008 onwards break them down.

- [X] T001 [US2] [US3] Tests first in `server/tests/Ecommerce.Identity.Tests/SignInThrottleTests.cs`. Cover:
  - the 6th sign-in after 5 wrong passwords is 429, even with the right password;
  - an unknown email trips at the same point;
  - simultaneous wrong passwords are all counted;
  - success clears the count, and so does a reset;
  - the block ends after the cool-down;
  - `SignInThrottled` is recorded only for a real account;
  - stale rows are purged;
  - forgot-password twice within a minute queues one email, including two at once.
- [X] T002 [US2] [US3] Shared `TooManyRequestsException` and its mapping in `GlobalExceptionHandler`. Then Identity:
  - `SignInThrottle` entity, configuration and migration;
  - `ISignInThrottle` and its repository;
  - `SignInOptions`;
  - the `LoginCommandHandler` and `ResetPasswordCommand` changes;
  - the forgot-password interval;
  - `SignInThrottleSweeper`.
- [X] T003 [US1] Tests first in the new project `server/tests/Ecommerce.ApiGateway.Tests`. Cover:
  - each policy trips at its limit with 429, `Retry-After` and ProblemDetails;
  - policies and IPs are separate buckets;
  - other routes are not limited;
  - a forged `X-Forwarded-For` from an untrusted peer is ignored;
  - a trusted proxy's header is honoured;
  - a bad configuration refuses to start.

  Then build `AuthRateLimits`, the routes and `Program.cs`.
- [X] T004 [US1] Compose: the `edge` network, the storefront's fixed address and `GATEWAY_TRUSTED_PROXIES`. Check `verify-storefront-image.sh` still passes.
- [X] T005 [US4] Storefront tests first, then:
  - `ApiError.retryAfterSeconds`;
  - the words;
  - 429 on the sign-in, sign-up, forgot-password and reset-password pages.
- [X] T006 Bruno check. Then end to end:
  - rapid reset requests through the storefront and the gateway;
  - 5 wrong passwords, then 429, for a real email and an unknown one.
- [X] T007 Mutation checks. Docs:
  - `docs/features/auth/security-best-practices.md`;
  - the reference, regenerated;
  - gateway and infrastructure pages;
  - timeline, backlog, decisions and counts;
  - CLAUDE.md.

---

## Phase 1: Foundational - the shared 429

- [X] T008 [P] Create `TooManyRequestsException(message, retryAfter)` with `RetryAfterSeconds` (rounded up, never zero) in `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/TooManyRequestsException.cs`
- [X] T009 Map it in `server/src/BuildingBlocks/Ecommerce.Shared/Middlewares/GlobalExceptionHandler.cs` to 429 with its message shown, a `Retry-After` header and a `retryAfter` extension
- [X] T010 [P] Add `Too_many_requests_says_how_long_to_wait` to `server/tests/Ecommerce.Identity.Tests/ForbiddenProblemTests.cs`

## Phase 2: User Story 1 - one client cannot hammer the anonymous auth endpoints (P1)

- [X] T011 [US1] Create `server/tests/Ecommerce.ApiGateway.Tests/Ecommerce.ApiGateway.Tests.csproj` and add it to `server/Ecommerce.slnx`
- [X] T012 [US1] Write the 12 tests in `server/tests/Ecommerce.ApiGateway.Tests/AuthRateLimitTests.cs` through WebApplicationFactory with unreachable destinations
- [X] T013 [US1] Write `AddAuthRateLimits` (three fixed-window policies partitioned by client IP, `RateLimits:<policy>:PermitLimit`/`WindowSeconds`, refuse to start on a value below 1, the 429 ProblemDetails in `OnRejected`) and `AddTrustedProxies` (`GATEWAY_TRUSTED_PROXIES`, `ForwardedHeaders.None` when empty, `ForwardLimit = 1`) in `server/src/ApiGateway/Ecommerce.ApiGateway/AuthRateLimits.cs`
- [X] T014 [US1] In `server/src/ApiGateway/Ecommerce.ApiGateway/Program.cs` register both, call `UseForwardedHeaders()` first and `UseRateLimiter()` before the proxy, and expose `public partial class Program` for the tests
- [X] T015 [US1] Add the six routes with their `RateLimiterPolicy` to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`
- [X] T016 [US1] In `server/docker-compose.app.yml` add the `edge` network (`172.30.10.0/24`), give the storefront `172.30.10.10`, and set `GATEWAY_TRUSTED_PROXIES: 172.30.10.10` on the gateway

## Phase 3: User Story 2 - guessing one account's password is slowed (P1)

- [X] T017 [US2] Create `SignInThrottle` in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/SignInThrottle.cs`, `SignInThrottleConfiguration`, the `SignInThrottles` set, and migration `20260925044212_AddSignInThrottles`
- [X] T018 [US2] Declare `ISignInThrottle`, `FailureOutcome` and `SignInOptions` (with `Problems()`) in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/SignInThrottling/SignInThrottling.cs`
- [X] T019 [US2] Implement `SignInThrottleRepository` (the pause check, the one-statement failure count, clear, purge) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/SignInThrottleRepository.cs`; bind and validate `SignIn` on start in `DependencyInjection.cs`
- [X] T020 [US2] In `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`: ask the pause first (429), count a wrong password or unknown email, record `SignInThrottled` for a real account when this call started the pause, clear on the right password
- [X] T021 [US2] Clear the count on a successful reset in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/PasswordReset/PasswordReset.cs`
- [X] T022 [US2] Write `SignInThrottleSweeper` in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/SignInThrottleSweeper.cs` and register it in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Program.cs` (`SignIn:PurgeMinutes`, 60)

## Phase 4: User Story 3 - an inbox cannot be flooded with reset links (P1)

- [X] T023 [US3] Add `AskedSinceAsync` (locks the user's row, then asks for a token since) to `IPasswordResetRepository` and `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/PasswordResetRepository.cs`; `ResetTokens.MinimumInterval` and the early return in the forgot handler
- [X] T024 [P] [US3] Adjust two tests in `server/tests/Ecommerce.Identity.Tests/PasswordResetTests.cs` for the one-a-minute interval

## Phase 5: User Story 4 - a person is told to wait, in their language (P2)

- [X] T025 [P] [US4] `ApiError.retryAfterSeconds` (body, then header) in `client/src/config/axios/api-error.ts`
- [X] T026 [US4] `tooManyAttempts` in `client/src/utils/shared/too-many.ts` (exported from `index.ts`) with `too-many.test.ts`; the words in `client/src/locales/{en,vi}/common.json`
- [X] T027 [US4] 429 on `client/src/pages/sign-in/refusal.ts`, `pages/sign-up`, `pages/forgot-password`, `pages/reset-password`, each with a test

## Phase 6: Polish

- [X] T028 [P] `bruno/security-checks/asking for reset links too fast is 429.yml`, last in its folder, its pre-request script catching the sandbox's 429
- [X] T029 Ten mutations, each caught ([quickstart.md](quickstart.md)); Identity 116/116, Gateway 12/12, client 313/313; the full Bruno collection 316/316 tests; `verify-storefront-image.sh`
- [X] T030 End to end on the compose stack with Identity, the gateway and the storefront rebuilt (output in [quickstart.md](quickstart.md) scenario 4)
- [X] T031 [P] Docs: security §4.7 and §5 (duplicate §4.5 renumbered), `db-design`, `email.md` rule 8, the error-mapping table, `microservices-design`, `running-in-containers`; decision 46; timeline, backlog, counts, `CLAUDE.md`; `docs/tools/generate_reference.py` shows each route's rate limit, and `docs/reference/{data-model,gateway}.md` are regenerated
- [X] T032 Merge through PR #145 (squash, 2026-09-25), closing #105

## Dependencies

T008-T010 before US2 (the pause throws the shared exception). US1 is independent of US2 and US3. T023 depends on
specs/061's repository. US4 depends only on the 429 shape. T028-T031 after; T032 last.

## Notes

- 32 tasks; the seven original ones are the summary, T008-T031 their breakdown.
