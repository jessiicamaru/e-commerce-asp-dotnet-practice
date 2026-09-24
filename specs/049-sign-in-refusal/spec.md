# Feature Specification: Sign-in refusal

**Feature Branch**: `049-sign-in-refusal` | **Created**: 2026-09-24 | **Issue**: #120

## Why

Specs/043 made Identity answer a locked or banned account with 403 and the reason, but only **after the
right password**. Before it, a locked account and a wrong password must look the same (#28). The
storefront throws that answer away: every failure other than 401 becomes "Signing in failed. Try again in
a moment." (`client/src/pages/sign-in/index.tsx:33`). A person locked for a week therefore retries,
believes the shop is broken, and never learns why or until when.

The server's sentence is also English only, with the end time in UTC. The storefront speaks Vietnamese
and English, and its readers live in UTC+7.

## User Scenarios

### US1 - A stopped person is told why and until when (P1)

A person locked for spam signs in with the right password. The page shows, in their language: "This
account is locked until 1 Oct 2026, 14:30: Spam in reviews". The time is in their own time zone. A banned
person reads "This account is banned: Fraud".

**Acceptance**
1. A locked account with the right password shows the end time in the reader's locale and time zone,
   with the reason, in Vietnamese or English.
2. A banned account with the right password shows the reason, in both languages.
3. A wrong password, on any account, locked or not, still shows only "That email and password do not
   match an account." (#28).
4. Any other 403 at sign-in shows the server's own sentence. A failure that carries no sentence shows
   the generic one.

### US2 - The refusal is data, not only a sentence (P1)

Identity's 403 carries `code` (`AccountLocked` or `AccountBanned`), `until` (ISO 8601 UTC, locks only)
and `reason` as ProblemDetails extensions, beside the English `detail`. A client words it itself, the way
a notification is worded from its kind and data (specs/042).

**Acceptance**
1. `POST /api/auth/login` for a locked account with the right password answers 403 with `code`,
   `until` and `reason`. For a banned account it answers with `code` and `reason`.
2. The facts are present in every environment, not only Development. They are written for the account's
   owner, as `detail` already is.

## Requirements

- **FR-001**: `ForbiddenException` may carry facts. The shared `GlobalExceptionHandler` writes them as
  extensions of the 403, and never lets them overwrite `traceId` or `errors`.
- **FR-002**: Identity's sign-in refusal carries `code`, `until` and `reason`, and keeps its `detail`.
- **FR-003**: The sign-in page words a 403 from `code`. For a 403 with an unknown code or none, it falls
  back to the server's `detail`, and then to the generic sentence.
- **FR-004**: Nothing changes before the password check. The 401 is the same for every account.

## Out of scope

- The refresh endpoint. A lock ends every session, refresh answers 401, and the person then meets
  this page.
- #112 (a lock taking up to 15 minutes to reach an access token already issued).
