# Feature Specification: Staff roles only in a back-office session

**Feature Branch**: `feat/278-staff-roles-back-office` | **Created**: 2026-10-03 | **Issue**: #278

**Status**: Draft

**Input**: Issue #278, "staff roles only in a back-office session". This is the fourth step of
[ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md), after the console moved to the back office
(specs/137).

## Why

The point of the back office is that a staff session never lives on the storefront's origin, where the public's words
are shown. Since specs/137 the console is in the back office. But a staff member who signs in to the storefront, with
their code, still receives a token carrying `Admin` or `Moderator`. Any script running on the storefront could act with
it, on every staff endpoint of every service. Until Identity stops writing staff roles into storefront sessions, the
separation is only a matter of where the buttons are.

## User Scenarios & Testing *(mandatory)*

### US1 - A storefront session never carries a staff role (Priority: P1)

**Acceptance Scenarios**:

1. **Given** an administrator signing in to the storefront with their password and code, **Then** their token carries their other roles (`Customer`, `Seller`) and no staff role, and every staff endpoint refuses it with 403.
2. **Given** the same person signing in to the back office, **Then** their token carries their staff role.
3. **Given** a session refreshed, **Then** it keeps the app it was made for. A storefront session never gains a staff role by refreshing.
4. **Given** a session from before this change, **Then** it is a storefront session. A staff member signs in to the back office again, once.

---

### US2 - Which app a session belongs to is decided by the browser, not the request body (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a sign-in, **Then** Identity takes the app from the request's `Origin`: a configured back-office origin (`BackOffice:Origins`) makes a back-office session; anything else, or none, makes a storefront session. No field of the body can say otherwise.
2. **Given** a script on the storefront's origin, **Then** it cannot make a back-office session. A browser writes `Origin` itself, and the back office's refresh cookie belongs to another host.

---

### US3 - Staff still find the back office from the storefront (Priority: P2)

**Acceptance Scenarios**:

1. **Given** a staff member on the storefront, whose session has no staff role, **Then** "Management platform" is still offered. The auth response says the account is staff (`staffAccount`), which the storefront uses to draw, never to decide.

---

### US4 - The tools that act as staff still work (Priority: P1)

**Acceptance Scenarios**:

1. **Given** `verify-auth.sh`, `verify-saga.sh`, Bruno, the seed scripts and Playwright, **Then** each signs in as staff by stating the back office's origin, and all pass.

### Edge Cases

- A staff account without two-factor sign-in is unchanged: no staff role in either app until it is set up (specs/110).
- Two-factor setup stays on the storefront, where such a person has a session.
- Revoking access tokens (specs/065) is unchanged. A role granted still arrives at the next refresh, in the back office.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `refresh_tokens.Client` (`Storefront` / `BackOffice`, nullable, expand-only). Null means `Storefront`.
- **FR-002**: The client is set when a session is created (password sign-in, the code, registration) from `Origin` against `BackOffice:Origins`, and carried across rotation.
- **FR-003**: `SessionRoles.Of` writes `Admin`/`Moderator` only for a session that is TOTP-verified **and** `BackOffice`. It stays the one place this is decided.
- **FR-004**: `AuthResponse.StaffAccount`. The storefront offers the back office when it is true.
- **FR-005**: The tools in US4 send the back office's `Origin` on their staff sign-in.
- **FR-006**: Docs: the auth and TOTP pages, moderation and staff, the back office page, ADR-003 progress, CLAUDE.md.

## Success Criteria *(mandatory)*

- **SC-001**: Against PostgreSQL, an administrator's storefront session has no staff role and their back-office session has one, both after the code and after a refresh.
- **SC-002**: The smoke jobs, Bruno and Playwright pass in CI.

## Assumptions

- Browsers send `Origin` on every `POST` (sign-in and refresh are `POST`s). A client that is not a browser can send any `Origin` it likes; it still needs the password and the code, which is the bar staff sessions already had.
