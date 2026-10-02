# Feature Specification: A one-time handoff from the storefront to the back office

**Feature Branch**: `feat/279-back-office-handoff` | **Created**: 2026-10-03 | **Issue**: #279

**Status**: Draft

**Input**: Issue #279, "one-time handoff from the storefront to the back office" - recorded in
[ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md) as the way to spare staff their password when
they cross from the storefront to the back office.

## Why

A staff member on the storefront who clicks "Management platform" lands on the back office's sign-in page and types
their email and password again, then their code. The two apps' sessions are separate on purpose (specs/136, 138), but
the password step adds nothing the person has not just proved. A single-use code, handed from the signed-in storefront
to the back office, can stand in for the password. The code from the authenticator stays: the back-office session is
still made by the person, in the back office, with their second factor.

## User Scenarios & Testing *(mandatory)*

### US1 - Staff cross to the back office with only their code (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a staff member signed in to the storefront, **When** they click "Management platform", **Then** the back office opens asking only for the code from their authenticator. With it they are in the console.
2. **Given** a back office already signed in, **When** they cross again, **Then** they land in the console without being asked anything.

---

### US2 - The handoff cannot be used to get in another way (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a handoff code, **Then** it works once, for 30 seconds. It is stored only as its hash, travels in the address's fragment (never sent to a server, never in a log or a `Referer`), and is removed from the address at once.
2. **Given** a code that is used, expired or made up, **Then** the answer is one 400, and the back office shows its ordinary sign-in.
3. **Given** a redeemed code, **Then** the answer is a two-factor challenge, never a session. The session is made only by the code from the authenticator, with the back office's `Origin` (specs/138).
4. **Given** a customer, or staff without two-factor sign-in, **Then** no handoff is issued: the first has no back office, the second must set up two-factor sign-in first.

### Edge Cases

- The challenge a handoff yields is the one a password yields: 5 minutes, 5 wrong codes, claimed once. A dead challenge restarts from the password.
- If the handoff cannot be issued (Identity down, a refusal), the link still opens the back office's sign-in. Nobody is stuck.
- Redeeming is anonymous and limited by the gateway like sign-in (specs/062).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `POST /api/auth/handoff` (signed in): issues `{ code }` for a staff account with two-factor sign-in, and stores only its SHA-256 (`back_office_handoffs`, 30 seconds, single use). Otherwise 403 with a `code`.
- **FR-002**: `POST /api/auth/handoff/redeem { code }` (anonymous, rate-limited): claims it with one guarded `UPDATE ... WHERE "UsedAt" IS NULL AND "ExpiresAt" > now RETURNING "UserId"`, and answers with a two-factor challenge exactly like sign-in's. Used, expired and made-up codes are one 400 on `Code`.
- **FR-003**: Storefront: "Management platform" issues a handoff and opens `<back office>/auth/callback#code=...`. Without a code it opens the back office.
- **FR-004**: Back office: `/auth/callback` removes the fragment, goes to the console if already signed in, else redeems and opens the sign-in form at the code step. Any failure opens the ordinary sign-in.
- **FR-005**: Audit (`BackOfficeHandoffIssued`), Bruno, Playwright, docs.

## Success Criteria *(mandatory)*

- **SC-001**: In a browser, a moderator signed in to the storefront reaches the back office's console by clicking "Management platform" and typing one code (Playwright).
- **SC-002**: A code works once, within 30 seconds, and never yields a session by itself (tests against PostgreSQL).

## Assumptions

- The code is 32 random bytes, base64url, so guessing it is not a concern. The rate limit is there for its own sake.
