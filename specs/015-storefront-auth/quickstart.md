# Quickstart: Sign Up, Sign In, Stay Signed In

> Written on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull request
> and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
cd ../client && npm ci && npm run dev          # http://localhost:5173
```

## Scenario 1 - The session through the proxy, with a cookie jar (US1, US3, US4, SC-001)

The path a browser takes. The cookie is `Secure`; curl sends it back over plain HTTP to `localhost`.

```bash
J=$(mktemp); E="qs-$(date +%s)@example.test"
curl -s -c $J -o /dev/null -D - -X POST http://localhost:5173/api/auth/register \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$E\",\"password\":\"Passw0rd!23\",\"firstName\":\"Q\",\"lastName\":\"S\"}" | grep -i 'HTTP/\|set-cookie'
curl -s -b $J -c $J -o /dev/null -w 'refresh %{http_code}\n' -X POST http://localhost:5173/api/auth/refresh
cp $J $J.before                                  # the cookie as it was BEFORE logout
curl -s -b $J -c $J -o /dev/null -D - -X POST http://localhost:5173/api/auth/logout | grep -i 'HTTP/\|set-cookie'
curl -s -b $J.before -o /dev/null -w 'refresh after logout %{http_code}\n' -X POST http://localhost:5173/api/auth/refresh
```

**Expected** (as the pull request recorded it):

```text
register via proxy     -> 200   set-cookie: refreshToken=...; path=/; secure; samesite=lax; httponly
refresh (a reload)     -> 200
logout                 -> 204   set-cookie: refreshToken=; expires=1970 ...
refresh after logout   -> 401
```

The last `refresh` sends the cookie as it was **before** logout cleared it, so a 401 proves the token
was deleted on the server rather than only dropped from the jar. Whether the pull request's run did
the same, or sent the cleared jar, is not recorded. (Newer Identity rules - confirmation and password policy - may refuse this exact body today; the flow is
as it was at the merge.)

## Scenario 2 - The tests (SC-001, SC-004)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~SessionTests"
```

**Expected**: `After_logout_the_refresh_token_no_longer_opens_a_session` and the three cases of
`Logging_out_without_a_valid_session_is_a_quiet_no_op` (null, empty, `not-a-real-token`) pass. The pull
request recorded Identity.Tests 24/24.

## Scenario 3 - Bruno

```bash
cd bruno
npx @usebruno/cli run auth --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `logout` → 204 and `refresh after logout is 401` → 401. The pull request recorded Bruno
53/53.

## Scenario 4 - In the browser (US1-US4, SC-002, SC-003)

1. Open <http://localhost:5173/sign-up>, create an account: the top bar shows your first name.
2. Reload: still signed in; DevTools → Application → Local Storage and Session Storage are empty; the
   Network tab shows one `refresh` on load.
3. Open `/account` directly in a new tab: "Checking your session…", then the page - no bounce to sign-in.
4. Sign out, reload: signed out.
5. Sign in with a wrong password, then with an unknown email: the same message both times.

**Not run at the merge** - the pull request states that nobody had clicked through the pages in a real
browser; the HTTP behaviour they rely on was exercised with curl (scenario 1).
