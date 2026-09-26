# Feature Specification: Sign-in refusal

> Completed on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature Branch**: `049-sign-in-refusal` | **Created**: 2026-09-24 | **Issue**: #120

**Status**: Merged (#130, 2026-09-24)

**Input**: Issue #120 - a person locked or banned by staff who signs in with the right password is told
"Signing in failed. Try again in a moment." and never learns why or until when.

## Why

Specs/043 made Identity answer a locked or banned account with 403 and the reason, but only **after the
right password**. Before it, a locked account and a wrong password must look the same (#28). The
storefront throws that answer away: every failure other than 401 becomes "Signing in failed. Try again in
a moment." (`client/src/pages/sign-in/index.tsx:33`). A person locked for a week therefore retries,
believes the shop is broken, and never learns why or until when.

The server's sentence is also English only, with the end time in UTC. The storefront speaks Vietnamese
and English, and its readers live in UTC+7.

## User Scenarios & Testing *(mandatory)*

### US1 - A stopped person is told why and until when (Priority: P1)

A person locked for spam signs in with the right password. The page shows, in their language: "This
account is locked until 1 Oct 2026, 14:30: Spam in reviews". The time is in their own time zone. A banned
person reads "This account is banned: Fraud".

**Why this priority**: This is the defect #120 reports. The refusal already existed on the server and
reached nobody, so a person stopped by staff could not tell a moderation decision from an outage.

**Independent Test**: Lock an account, sign in on the storefront with its right password in each language,
and read the alert: the reason and a local end time are shown, not the generic sentence.

**Acceptance Scenarios**:

1. **Given** a locked account, **When** its owner signs in with the right password, **Then** the page shows
   the end time in the reader's locale and time zone, with the reason, in Vietnamese or English.
2. **Given** a banned account, **When** its owner signs in with the right password, **Then** the page shows
   the reason, in both languages.
3. **Given** any account, locked or not, **When** somebody signs in with a wrong password, **Then** the page
   still shows only "That email and password do not match an account." (#28).
4. **Given** a 403 at sign-in that the page has no words for, **When** it arrives, **Then** the page shows the
   server's own sentence; **Given** a failure that carries no sentence (a 500, or no answer at all),
   **Then** it shows the generic one.

---

### US2 - The refusal is data, not only a sentence (Priority: P1)

Identity's 403 carries `code` (`AccountLocked` or `AccountBanned`), `until` (ISO 8601 UTC, locks only)
and `reason` as ProblemDetails extensions, beside the English `detail`. A client words it itself, the way
a notification is worded from its kind and data (specs/042).

**Why this priority**: US1 cannot be done honestly without it - parsing an English sentence to find a date
and a reason is the fragile alternative. It is equal in priority because the two ship together.

**Independent Test**: `POST /api/auth/login` through the gateway for a locked account with its right
password, against a service running in Production mode, and read the body.

**Acceptance Scenarios**:

1. **Given** a locked account, **When** `POST /api/auth/login` is sent with the right password, **Then** it
   answers 403 with `code`, `until` and `reason`. **Given** a banned account, **Then** it answers with
   `code` and `reason` and no `until`.
2. **Given** any environment, not only Development, **When** the refusal is written, **Then** the facts are
   present. They are written for the account's owner, as `detail` already is.
3. **Given** a fact whose name is one the shared handler writes itself (`traceId`, `errors`), **When** the
   403 is written, **Then** the handler's value stays and the fact is dropped.

### Edge Cases

- **Wrong password on a locked account.** Still the bare 401 with one message for every account; the
  facts exist only after the password is right (#28).
- **A 403 with an unknown `code`, or none.** The page shows the server's `detail`; it never invents a
  sentence for a code it does not know.
- **A 403 with no body at all, or a network failure.** The generic "Signing in failed" sentence.
- **A lock that is also a ban.** The ban wins, as it already did for `detail`: `code` is `AccountBanned`
  and no `until` is sent.
- **Microseconds.** PostgreSQL keeps `LockedUntil` to the microsecond; `until` is that instant as UTC, and the
  page shows it to the second in the reader's locale.
- **An older client, Bruno or curl.** Reads `detail`, which is unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ForbiddenException` may carry facts. The shared `GlobalExceptionHandler` writes them as
  extensions of the 403, and never lets them overwrite `traceId` or `errors`.
- **FR-002**: Identity's sign-in refusal carries `code`, `until` and `reason`, and keeps its `detail`.
- **FR-003**: The sign-in page words a 403 from `code`. For a 403 with an unknown code or none, it falls
  back to the server's `detail`, and then to the generic sentence.
- **FR-004**: Nothing changes before the password check. The 401 is the same for every account.
- **FR-005**: The facts are written in every environment, like the message of a `ForbiddenException`, so
  nothing may be put in them that the message could not already say.
- **FR-006**: `until` is serialised as ISO 8601 in UTC (a trailing `Z`); the client converts it to the
  reader's time zone.

### Key Entities

- **Refusal facts**: a small dictionary carried by `ForbiddenException` - for sign-in, `code`, `until` and
  `reason`. Not stored anywhere; it is read from the `users` row (`LockedUntil`, `LockReason`, `BanReason`)
  at the moment of refusal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A locked or banned person who types the right password reads the reason (and, for a lock, the
  end time in their own time zone) in the language the storefront is set to - verified in both languages
  by the page tests.
- **SC-002**: A wrong password produces a response byte-for-byte as before for every account: the same 401
  and the same message.
- **SC-003**: The facts reach the HTTP body when the service runs in Production, verified by a handler test
  and end to end through the gateway.
- **SC-004**: No existing field of the 403 changes: a client that reads only `detail` sees what it saw
  before.

## Assumptions

- The reason text is what staff typed; it is shown as written, in whatever language they wrote it, and is
  not translated.
- The reader's time zone is the browser's; the storefront shows every other date the same way
  (`toLocaleString(i18n.language)`).

## Out of scope

- The refresh endpoint. A lock ends every session, refresh answers 401, and the person then meets
  this page.
- #112 (a lock taking up to 15 minutes to reach an access token already issued). Closed later by
  specs/065.
