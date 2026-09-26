# Feature Specification: Email delivery

> Completed on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature Branch**: `087-email-delivery` | **Created**: 2026-09-27 | **Status**: Merged (#179, 2026-09-26 UTC) | **Issue**: #175 (closes it)

**Input**: Issue #175 - a failed email was recorded with its reason, and nothing read it. The issue asked whether sent
emails should be listed too.

## Why

An email that could not be delivered ends `Failed` in `outgoing_emails`, with its last error (specs/060). That
happens after 12 attempts with the mail server down, or at once for an unknown recipient or missing words.
Nothing read those rows. The person never got their confirmation, reset link or lock notice, and nobody but a
database user could find out why.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An administrator sees which emails failed and why (Priority: P1)

A customer writes to support: "I never got my order confirmation." An administrator opens
`/admin/email-delivery`, which opens on the failed emails, searches for the customer's address, and sees the
confirmation with its template, language, attempts and last error.

**Why this priority**: Without it a failure is invisible to anybody without database access. Seeing it is the
precondition for doing anything about it.

**Independent Test**: With a failed email in `outgoing_emails`, `GET /api/emails` as an administrator lists it with
its recipient's address, template, language, attempts and last error, and no data.

**Acceptance Scenarios**:

1. **Given** failed emails exist, **When** an administrator opens the list without choosing a state, **Then** it
   shows the failed ones, newest first, a page at a time.
2. **Given** a search `lan@`, **When** the list is read, **Then** only emails to addresses containing it are shown.
3. **Given** a state `Pending` or `Sent`, **When** the list is read, **Then** only emails in that state are shown.
4. **Given** any email, **When** it is listed, **Then** its data is never in the response.
5. **Given** a moderator, **When** they ask for the list, **Then** the answer is 403; without a token, 401.

---

### User Story 2 - An administrator sends a failed email again (Priority: P2)

The mail server was down for an afternoon and a dozen order confirmations failed. Once it is back, an administrator
presses "Send again" on each; they go out within the dispatcher's next sweep.

**Why this priority**: Seeing a failure is half of support; the other half is fixing the ones that can be fixed.
Second because it depends on the list.

**Independent Test**: Retry a failed email; it becomes `Pending`, due now, with 0 attempts and no last error; the
dispatcher sends it once; a second retry is 409.

**Acceptance Scenarios**:

1. **Given** a failed email, **When** an administrator retries it, **Then** it is back in the queue from its first
   attempt, due now, its last error cleared, and the retry is in the audit log as `System` / `EmailRetried`.
2. **Given** an email that is not failed (pending or sent), **When** it is retried, **Then** the answer is 409 and it
   is not moved.
3. **Given** two administrators retry the same email at once, **When** both arrive, **Then** one moves it and the
   other gets 409.
4. **Given** an unknown id, **When** it is retried, **Then** the answer is 404 `Email not found.`.

---

### User Story 3 - A dead link is never resent (Priority: P3)

A password reset email failed an hour ago. The page shows it without "Send again", and a direct retry is refused in
words: the person should ask for a new link.

**Why this priority**: A reset link lives 30 minutes; sending it late sends a link that no longer works, and its
data - the token - was the one thing not to keep around. Third because it narrows story 2.

**Independent Test**: A failed `PasswordReset` email lists with `canRetry: false`, and a retry is 409.

**Acceptance Scenarios**:

1. **Given** a failed reset or confirmation email, **When** it is listed, **Then** `canRetry` is false and the page
   offers no retry.
2. **Given** the same email, **When** a retry is sent anyway, **Then** the answer is 409
   `A reset or confirmation link expires; the person asks for a new one instead.`

---

### Edge Cases

- **Sent emails.** Listed too (the issue's question): an administrator already reads every order, and "did the
  confirmation go out?" is the first support question.
- **An email whose recipient's account is gone.** Still listed, with no address (a left join).
- **An email with data that is a token.** Never shown; the reset and confirmation data is also scrubbed to `{}`
  once sent (specs/061).
- **A search that matches nobody.** An empty page, `totalCount: 0`.
- **An unknown state name.** 400 `Status is Pending, Sent or Failed.`; page size must be 1-50; search at most 255.
- **The email's history.** `outgoing_emails` keeps one current state; the attempts and last error a retry cleared
  are kept as the audit entry's "before".
- **Nobody is alerted when an email fails.** Out of scope; an administrator sees it by opening the page.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** `GET /api/emails` (Admin, not Moderator) lists the emails in one state, `Failed` by default, then
  `Pending` or `Sent`, newest first and paged, optionally for recipients whose address contains a search.
  - Each email shows its recipient and their address, template, language, state, attempts, last error, times,
    and whether it can be sent again.
  - It **never** shows the email's data: a reset or confirmation email's data is a token.
- **FR-002** `POST /api/emails/{id}/retry` (Admin) puts a failed email back in the queue.
  - The email is due now, from its first attempt, with its last error cleared.
  - It is one guarded `UPDATE ... WHERE "Status" = 'Failed'`: an email that is not failed is a 409 and is not
    moved.
  - The retry is audited as `System` / `EmailRetried`.
- **FR-003** A reset or confirmation email is never retried: `canRetry: false`, and a 409 in words. The link has
  expired by the time anybody looks, and the person asks for a new one.
- **FR-004** The storefront has `/admin/email-delivery`. It shows tabs per state, a search, the reason for each
  failure, and "Send again" where allowed.
- **FR-005** The gateway routes `/api/emails` and `/api/emails/{**catch-all}` to Identity.

### Key Entities

- **Outgoing email** (`outgoing_emails`, specs/060): one row per email with its current state, attempts, next
  attempt, last error and sent time; unchanged in shape.
- **Email delivery view** (`OutgoingEmailResponse`): an outgoing email as an administrator sees it, with the
  recipient's address and `canRetry`, without the data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A failed email is listed with why and whom, and no response of the list ever carries data (Bruno checks
  every sent email of a run has neither `data` nor `dataJson`).
- **SC-002**: A retried failed email goes out exactly once and is in the audit log; a second retry is 409.
- **SC-003**: A reset link is never retried; an unknown id is 404.
- **SC-004**: A moderator is refused with 403 and an anonymous caller with 401.
- **SC-005**: Three mutations (no guard, a reset link retried, the data returned) each turned `EmailDeliveryTests`
  red (from the pull request).

## Assumptions

- Only administrators read who was written to about what, like the email words (specs/077).
- The dispatcher (specs/060) picks up any `Pending` email whose `NextAttemptAt` has passed, so "back in the queue"
  needs nothing but the row.

## Decisions

- **Sent emails are listed too.** The issue asked whether they should be, since the list records who bought what.
  An administrator already reads every order, so this discloses nothing new, and "did the confirmation go out?" is
  the first support question.
- **The retried email is in the audit log, not the email's own history.** `outgoing_emails` keeps one current
  state. The attempts and the error it had are the audit entry's "before".

The full reasoning is in [research.md](./research.md).

## Out of scope

- Alerting anyone when an email fails.
