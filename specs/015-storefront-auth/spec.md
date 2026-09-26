# Feature Specification: Sign Up, Sign In, Stay Signed In

> Completed on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull
> request and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature Branch**: `015-storefront-auth` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#44](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/44), 2026-09-22 (02:45, UTC+7)

**Input**: Issue [#35](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/35), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

The token story - short access token in the body, refresh token in an HttpOnly cookie - was argued
for in `docs/features/auth/security-best-practices.md` and had never met a browser.

## What building it found

**There was no way to sign out.** The refresh token is an HttpOnly cookie no script can delete, so a
client that only forgets its access token is signed straight back in by the next silent refresh.
`POST /api/auth/logout` now deletes the refresh token server-side and clears the cookie. That is a
backend change, made here because the feature cannot be correct without it.

**Registration validates nothing** - `not-an-email`, a one-character password and empty names create
an account. Filed as [#43](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/43)
rather than worked around in the client.

## User Scenarios & Testing

### User Story 1 - A visitor creates an account and is signed in (Priority: P1)

**Why this priority**: nothing a customer does later - a cart, an address, an order - is possible
without an account.

**Independent Test**: fill the sign-up form with a new email; the top bar shows the person's first
name.

**Acceptance Scenarios**:

1. **Given** a new email, **When** the form is sent, **Then** the account exists, the person is signed
   in, and a `refreshToken` cookie is set `HttpOnly; Secure; SameSite=Lax`.
2. **Given** an email already registered, **When** the form is sent, **Then** the page says so (409)
   instead of failing silently.
3. **Given** a 400 from Identity with field errors, **When** the form is sent, **Then** each message
   appears beside its field.

### User Story 2 - A customer signs in, and a wrong password gives nothing away (Priority: P1)

**Why this priority**: equal first; returning customers are most of the traffic.

**Independent Test**: sign in with the right password, then with a wrong one and with an unknown
email.

**Acceptance Scenarios**:

1. **Given** the right password, **When** the person signs in, **Then** they return to the page they
   were sent from, or `/`.
2. **Given** a wrong password or an unknown email, **When** they sign in, **Then** both read "That email
   and password do not match an account." - one message, so the page does not reveal which emails have
   accounts (#28).

### User Story 3 - A reload keeps the person signed in, with no token in storage (Priority: P1)

**Why this priority**: equal first; it is the property the design argued for and nobody had tested.

**Independent Test**: sign in, reload; still signed in. Inspect `localStorage` and `sessionStorage`:
empty.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** the page reloads, **Then** one `POST /api/auth/refresh` with
   the cookie restores the session.
2. **Given** an expired access token, **When** any request answers 401, **Then** the client refreshes
   once and retries that request once.
3. **Given** several requests failing with 401 at the same moment, **When** they refresh, **Then** they
   share one refresh call.

### User Story 4 - Signing out really ends the session (Priority: P1)

**Why this priority**: equal first, and the one the backend could not do: without it, "sign out" was a
lie undone by the next reload.

**Independent Test**: sign out, then call `POST /api/auth/refresh` with the old cookie: 401.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** they sign out, **Then** the refresh token is deleted on the
   server, the cookie is cleared, and a later refresh is 401.
2. **Given** no session, an unknown token or an already-deleted one, **When** logout is called,
   **Then** it answers 204 and changes nothing.
3. **Given** an expired access token, **When** the person signs out, **Then** it still works - logout
   needs no access token.

### Edge Cases

- **A page that needs a customer, opened on a reload.** It waits for the silent refresh to answer
  ("Checking your session…") before deciding to send the person to sign-in; otherwise every reload of
  such a page would bounce a signed-in customer.
- **The refresh fails** (cookie gone, token deleted): the client forgets the user and the token.
- **Signing out while offline or with Identity down**: the client forgets the session anyway (`finally`),
  but the cookie still opens a session on the server - not otherwise handled.
- **The access token already issued keeps working until it expires** (15 minutes): logout is stateless
  for access tokens (Bruno's `logout` docs say so). Revoking access tokens came much later (specs/065).

## Requirements

- **FR-001**: A person can create an account and is signed in.
- **FR-002**: A person can sign in; a wrong password shows one message whether or not the email exists.
- **FR-003**: The access token lives in memory only; a reload restores the session through the refresh cookie.
- **FR-004**: A 401 on any request triggers one silent refresh and one retry; concurrent 401s share it.
- **FR-005**: Signing out ends the session on the server; a refresh afterwards is 401.
- **FR-006**: Pages that need a customer wait for the session to be restored before redirecting.
- **FR-007**: Logout MUST be anonymous, idempotent and always 204, so an expired access token or a
  missing cookie never stops somebody signing out.
- **FR-008**: After sign-in the person MUST return to the page that sent them there.

### Key Entities

- **Session (client)** - the signed-in user (id, email, first and last name) and the access token, in
  memory only; `restoring` while the first refresh is in flight.
- **Refresh token (Identity)** - a row in `refresh_tokens`, carried by the HttpOnly `refreshToken`
  cookie; valid 7 days; deleted by logout.

## Success Criteria

- **SC-001**: After logout, a refresh with the same cookie is 401 in 100% of attempts
  (`SessionTests`, Bruno `refresh after logout is 401`).
- **SC-002**: No token appears in `localStorage` or `sessionStorage` at any point.
- **SC-003**: A reload of a signed-in page keeps the person signed in with one refresh request.
- **SC-004**: Logout without a session is a quiet no-op for a missing, empty or unknown token.

## Assumptions

- Identity already issues a 15-minute access token in the body and a 7-day refresh token in an HttpOnly
  cookie, and replaces the refresh token with a new one on every refresh.
- The storefront runs behind the Vite proxy, one origin (specs/014), so the cookie is first-party.
- #28 has made a wrong password a 401 rather than a 500.

## Out of Scope

- Registration validation - filed as #43, not worked around.
- Revoking access tokens already issued, "sign out everywhere", password reset, email confirmation -
  later features (specs/061, 063, 065).
- Remembering where a person was across a sign-up (only sign-in returns them).
