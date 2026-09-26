# Implementation Plan: Limits on the sign-in and email endpoints

> Completed on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Branch**: `062-auth-rate-limits` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #105

## Summary

Three layers, each answering 429 with how long to wait. The gateway limits the anonymous auth endpoints per
client IP with ASP.NET Core's rate limiter, attached per YARP route, and believes `X-Forwarded-For` only from a
configured proxy (the storefront's nginx). Identity pauses one email for 5 minutes after 5 wrong passwords in 15,
counted in a new `sign_in_throttles` table by single atomic statements, and sends at most one reset email a
minute per address. The storefront words the wait in minutes, in both languages. Decisions are below and in
[research.md](research.md) (decision 46 in [docs/project/decisions.md](../../docs/project/decisions.md)).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19 in the storefront

**Primary Dependencies**: `Microsoft.AspNetCore.RateLimiting` / `System.Threading.RateLimiting` (fixed window), YARP
`RateLimiterPolicy`, `Microsoft.AspNetCore.HttpOverrides` (forwarded headers), EF Core raw SQL on Npgsql,
`Microsoft.AspNetCore.Mvc.Testing` for the new gateway test project

**Storage**: New table `sign_in_throttles` in `ecommerce_identity_db` (5435), migration
`20260925044212_AddSignInThrottles`. The gateway's counters are in memory

**Testing**: `Ecommerce.ApiGateway.Tests` (new, 12 tests, no database: the real pipeline through
WebApplicationFactory with unreachable destinations - 502 means let through, 429 means refused);
`SignInThrottleTests` (14) against real PostgreSQL; Vitest (13 new); Bruno; an end-to-end run on the compose stack

**Target Platform**: Gateway (5000), Identity (5056), the storefront container (nginx on 8088, `172.30.10.10` on the
new `edge` network)

**Project Type**: Security fix across the gateway, Identity, Shared and the storefront

**Performance Goals**: None; one extra statement per sign-in (the pause check) and one per wrong password

**Constraints**: #28 - an unknown email behaves exactly like a real one; simultaneous wrong passwords all counted;
no limit keyed on a header the caller writes; a bad setting refuses to start rather than switching a limit off

**Scale/Scope**: Six gateway routes, three policies, one table, four storefront pages

## Design

### 1. The gateway, per client IP (US1)

`AuthRateLimits` in the gateway project adds three fixed-window policies, each partitioned by client IP:

| Policy | Routes | Default |
| :-- | :-- | :-- |
| `sign-in` | `/api/auth/login`, `/register`, `/register-seller`, `/reset-password` | 30 a minute |
| `email` | `/api/auth/forgot-password` | 5 a minute |
| `session` | `/api/auth/refresh` | 60 a minute |

- **Configuration.** The numbers live under `RateLimits:<policy>:PermitLimit` / `WindowSeconds`, and a
  missing or non-positive value refuses to start.
- **Routes.** The limits attach through YARP: dedicated routes for the six paths, each with a
  `RateLimiterPolicy`. They are more specific than `/api/auth/{**catch-all}`, so they win it. Wiring is
  `app.UseRateLimiter()` before `MapReverseProxy()`.
- **Rejection.** `OnRejected` writes 429 `application/problem+json` (`title`, `status`, `detail`,
  `retryAfter`) with `Retry-After`, taken from the lease's `RetryAfter` metadata.
- **Client IP.** `RemoteIpAddress` after `UseForwardedHeaders` (X-Forwarded-For, `ForwardLimit = 1`).
  - `KnownProxies` and `KnownNetworks` are **cleared**, then filled from `GATEWAY_TRUSTED_PROXIES`: IPs
    or CIDRs, comma-separated.
  - With nothing configured, nobody is trusted and the header is ignored.
  - Compose gives the storefront a fixed address on a small `edge` network (`172.30.10.10`), and the
    gateway trusts exactly that.

### 2. Identity, per email (US2)

- **The table.** `sign_in_throttles`:
  - `EmailKey` varchar(255), primary key: `EmailKey.For`, the same key sign-in looks up by;
  - `Failures` int;
  - `WindowStartedAt` timestamptz;
  - `BlockedUntil` timestamptz, nullable.
- **The repository.** `ISignInThrottle` has four methods:
  - `BlockedUntilAsync(key, now)`;
  - `RecordFailureAsync(key, now)`: one `INSERT ... ON CONFLICT DO UPDATE ... RETURNING`. It restarts
    the window when it is older than 15 minutes, otherwise adds one. When the count reaches 5 it sets
    `BlockedUntil = now + 5 min` and **resets the count to 0**, so after the cool-down another 5 wrong
    passwords are needed. It returns whether this call tripped the block;
  - `ClearAsync(key)`;
  - `PurgeStaleAsync(now)`.
- **`LoginCommandHandler`**, in this order:
  1. blocked → `TooManyRequestsException` (no audit entry, which would flood the log);
  2. a wrong password or an unknown email → record the failure, then `SignInThrottled` if it tripped for
     a real account, then the existing `SignInRefused` and 401;
  3. the right password → clear the count, then the existing lock and ban checks.
- **`ResetPasswordCommand`** clears the count for the account's email.
- **Purging.** `SignInThrottleSweeper` runs hourly, like the other sweepers, and deletes rows with no
  block running and a window older than 15 minutes.
- **Settings.** `SignIn:MaxFailures` 5, `SignIn:WindowMinutes` 15 and `SignIn:CooldownMinutes` 5, all
  validated.

### 3. The shared 429

`Ecommerce.Shared.Exceptions.TooManyRequestsException(message, retryAfter)`. `GlobalExceptionHandler`
maps it to 429, shows the message in every environment (like `ForbiddenException`), and writes
`Retry-After` plus a `retryAfter` extension, in whole seconds rounded up.

### 4. Forgot-password, per address (US3)

Inside the existing transaction, the handler:
1. locks the user's row (`SELECT ... FOR UPDATE`);
2. returns silently if a token for that user was created in the last `PasswordReset:MinimumIntervalSeconds`
   (60).

Two requests at once therefore queue one email.

*Correction (2026-09-27):* the interval is not a `PasswordReset:MinimumIntervalSeconds` setting. The code has a
constant, `ResetTokens.MinimumInterval` = one minute, checked through `IPasswordResetRepository.AskedSinceAsync`,
which locks the user's row and asks whether a token was created since. Likewise, in section 1 the list the code
clears and fills is `KnownIPNetworks` (the .NET 10 name), not `KnownNetworks`.

### 5. Storefront (US4)

- `ApiError.retryAfterSeconds` reads `problem.retryAfter`, then the `Retry-After` header.
- `common:tooManyAttempts_one` / `_other` hold the words.
- `describeSignInFailure` and the sign-up, forgot-password and reset-password pages handle 429.

### 6. Bruno

`security-checks/asking for reset links too fast is 429`:
- its pre-request script sends five `forgot-password` requests;
- the request itself is the sixth, and must be 429 with `Retry-After`;
- it is the **last** request of `security-checks`, and it only uses the `email` bucket, which nothing
  after it touches.

## Decisions

1. **Per email, not per account, and not a lock.** An account-row counter would answer differently for
   unknown emails (#28). A moderation lock would hand anybody a way to shut an account for 30 minutes or
   more. The known limit, recorded: somebody who knows an email can keep its sign-in delayed with 5 wrong
   passwords every 5 minutes. They cannot get in, and the per-IP limit caps how many emails one client can
   do that to.
2. **In-memory counters at the gateway, database counters in Identity.** The gateway's limits are about
   one client's rate, and a restart forgiving them costs nothing. The per-email count is about one
   account under attack from anywhere, so it must survive restarts and be shared by every Identity
   instance.
3. **`X-Forwarded-For` only from configured proxies.** Trusting it from anyone makes every limit
   optional: the caller writes a new address on each request.
4. **A new test project for the gateway** (`Ecommerce.ApiGateway.Tests`, WebApplicationFactory, no
   database). The limits are configuration plus middleware order, and only a request through the real
   pipeline proves them. Allowed requests go to an unreachable destination and get 502; refused ones get
   429 without ever reaching it.

## Constitution check

- **I. Service Autonomy.** Identity keeps its own throttle table; the gateway holds no business data.
  Pass.
- **II. Clean Architecture.** The interface is in Application, the SQL in Infrastructure. Pass.
- **III. Atomic writes.** Every counter change is one atomic statement. The audit entry goes through the
  outbox with the refusal's existing save. Pass.
- **IV. Identity from the token.** There is no token on these anonymous endpoints: the key is the typed
  email, which is the thing under attack. Pass.
- **V. Evidence.** Tests go through the real gateway pipeline and real PostgreSQL, plus mutation checks
  and an end-to-end run. Pass.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The list above is the check as first
written; this table states the same verdicts in the standard form.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity keeps its own throttle table; the gateway holds no business data, only in-memory counters of request rate. No service reads another's database |
| **II. Clean Architecture Layering** | **Pass.** `ISignInThrottle`, `SignInOptions` and the handler changes are in Application; the SQL is in `SignInThrottleRepository` (Infrastructure); `TooManyRequestsException` and its mapping are in `Ecommerce.Shared`; the gateway's policies are one static class wired in `Program.cs` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Every counter change is one atomic statement (`INSERT ... ON CONFLICT DO UPDATE ... RETURNING`), so simultaneous failures are all counted and one starts the pause. The `SignInThrottled` entry goes through the outbox with the refusal's existing save. The reset interval is decided under a row lock inside the reset's transaction |
| **IV. Identity Comes From the Token** | **Pass.** There is no token on these anonymous endpoints: the key is the typed email, which is the thing under attack, and the client IP comes from the connection or a configured proxy - never from a header the caller controls |
| **V. Evidence Over Assumption** | **Pass.** Tests go through the real gateway pipeline and real PostgreSQL; ten mutations were each caught; an end-to-end run on the compose stack is recorded in PR #145. The empty-trust-list default was found by a test, not assumed |

**Post-design re-check**: no violations. Migration: a new table only; an earlier image ignores it.

## Project Structure

### Documentation (this feature)

```text
specs/062-auth-rate-limits/
├── spec.md
├── plan.md                  # This file
├── research.md              # Seven decisions
├── data-model.md            # sign_in_throttles
├── quickstart.md
├── contracts/
│   └── http-api.md          # the 429, the routes and their policies, the sign-in pause, the reset interval
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #145)

```text
server/src/ApiGateway/Ecommerce.ApiGateway/
├── AuthRateLimits.cs                      # policies, trusted proxies, the 429 body
├── Program.cs                             # UseForwardedHeaders, UseRateLimiter; partial Program for tests
└── appsettings.json                       # six routes with RateLimiterPolicy
server/src/BuildingBlocks/Ecommerce.Shared/
├── Exceptions/TooManyRequestsException.cs
└── Middlewares/GlobalExceptionHandler.cs  # 429 + Retry-After + retryAfter
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/SignInThrottle.cs
├── Ecommerce.Identity.Application/Auth/SignInThrottling/SignInThrottling.cs   # ISignInThrottle, SignInOptions
├── Ecommerce.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs
├── Ecommerce.Identity.Application/Auth/Commands/PasswordReset/PasswordReset.cs # interval; clear on reset
├── Ecommerce.Identity.Infrastructure/Configurations/SignInThrottleConfiguration.cs
├── Ecommerce.Identity.Infrastructure/Migrations/20260925044212_AddSignInThrottles.cs
├── Ecommerce.Identity.Infrastructure/Persistence/{ApplicationDbContext.cs,SignInThrottleSweeper.cs}
├── Ecommerce.Identity.Infrastructure/Persistence/Repositories/{SignInThrottleRepository,PasswordResetRepository}.cs
├── Ecommerce.Identity.Infrastructure/DependencyInjection.cs
└── Ecommerce.Identity.WebApi/Program.cs    # the sweeper
server/tests/Ecommerce.ApiGateway.Tests/{AuthRateLimitTests.cs,Ecommerce.ApiGateway.Tests.csproj}   # new project
server/tests/Ecommerce.Identity.Tests/{SignInThrottleTests,ForbiddenProblemTests,PasswordResetTests,IdentityTestFixture}.cs
server/Ecommerce.slnx, server/docker-compose.app.yml       # edge network, 172.30.10.10, GATEWAY_TRUSTED_PROXIES
client/src/config/axios/api-error.ts                        # retryAfterSeconds
client/src/utils/shared/{too-many.ts,too-many.test.ts,index.ts}
client/src/pages/{sign-in/refusal.ts,sign-up,forgot-password,reset-password}/  # 429 and tests
client/src/locales/{en,vi}/common.json
bruno/security-checks/asking for reset links too fast is 429.yml
```

Documentation touched in the same change: `CLAUDE.md`, `docs/architecture/{error-handling-and-shared-building-block,microservices-design}.md`,
`docs/features/auth/{db-design,security-best-practices}.md`, `docs/features/email.md` (rule 8),
`docs/infrastructure/running-in-containers.md`, `docs/overview/project-overview.md`,
`docs/project/{backlog,decisions,timeline}.md`, `docs/reference/{data-model,gateway}.md` (regenerated),
`docs/testing/testing-strategy.md`, and `docs/tools/generate_reference.py` (now shows each route's rate limit).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty. The new test project is a decision
> (decision 4 above), not a violation.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Several gateway instances** each count separately; the limits assume one.
- **A known email can be kept paused** by somebody willing to send 5 wrong passwords every 5 minutes (decision 1).
- Nobody is told by email that their password is being guessed; there is no CAPTCHA.
- Signed-in endpoints are not limited; a wrong *current* password on a password change joined the same pause in
  [specs/064](../064-account-settings/).
