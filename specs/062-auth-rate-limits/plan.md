# Implementation Plan: Limits on the sign-in and email endpoints

**Branch**: `062-auth-rate-limits` | **Spec**: [spec.md](spec.md) | **Issue**: #105

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
