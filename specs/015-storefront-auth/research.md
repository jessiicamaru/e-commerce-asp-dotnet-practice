# Research: Sign Up, Sign In, Stay Signed In

> Written on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull request
> and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

## D1 - The access token in memory only

**Decision**: a module-level variable in `AuthContext.tsx`, read synchronously by the call layer through
`configureAuth`. Never `localStorage` or `sessionStorage`.

**Rationale** (the file's own comment): "anything a script can read, an injected script can steal. The
refresh token is Identity's HttpOnly cookie, which no script can read at all; on a reload the
storefront asks for a fresh access token with it." This is the storefront-wide decision taken on #34.

**Alternatives considered**: storing the access token in web storage to survive a reload. Rejected for
the reason above; the reload is handled by D2 instead.

## D2 - Restore on load with one refresh, and share one refresh between concurrent 401s

**Decision**: on mount the provider calls `POST /api/auth/refresh` (anonymous, credentials included);
`restoring` is true until it answers. A `useRef` holds the in-flight refresh promise, so any 401 that
arrives while a refresh is running awaits the same one (`refreshing.current ??= ...`), and it is
cleared in `finally`.

**Rationale**: Identity replaces the refresh token with a new one on every refresh. Two refreshes racing
with the same cookie would have the second present a token the first had already removed, and be
refused - signing a person out for having two requests in flight.

**Alternatives considered**: a refresh per failed request. Rejected for the race above. A timer that
refreshes before expiry - not recorded as considered.

## D3 - Sign-out ends the session on the server: `POST /api/auth/logout`

**Decision**: a new Identity endpoint. It reads the `refreshToken` cookie, deletes that token's row
through `LogoutCommand`, deletes the cookie with the same `HttpOnly; Secure; SameSite=Lax` options it was
set with, and answers 204 - always. The client calls it first and forgets its own state in `finally`.

**Rationale**: "The refresh token is an HttpOnly cookie no script can delete, so a client that only
forgets its access token is signed straight back in by the next silent refresh." Anonymous because "an
expired access token must not stop someone signing out"; always 204 because "signing out must never
fail in a way the person has to deal with" (handler comment).

**Alternatives considered**:

- **Forget the token in the client only.** Rejected: the next reload signs the person back in.
- **Require the access token.** Rejected: a person whose access token has expired could not sign out.
- **Revoke instead of delete** (set `RevokedAt`, which the refresh handler already checks). Not recorded
  as considered; the handler removes the row, as the refresh handler does when it rotates.

## D4 - One message for a wrong password and an unknown email

**Decision**: the sign-in page shows "That email and password do not match an account." for any 401,
and a generic retry message for anything else.

**Rationale**: Identity answers both cases with the same 401 (#28), "so the page does too - it must not
reveal which emails have accounts" (the page's comment).

**Alternatives considered**: distinct messages. Rejected for the enumeration risk.

## D5 - File what the backend lacks, do not work around it

**Decision**: registration's missing validation - `not-an-email`, a one-character password and empty
names all create an account - is filed as #43. The sign-up page shows Identity's field errors when
there are any, and nothing more.

**Rationale**: the storefront-wide rule on #34: "Anything the interface needs that the backend lacks is
filed as its own issue, not worked around in the client." A client-side check would have hidden the
defect from every other client.

**Alternatives considered**: validating in the form. Rejected for the reason above.
