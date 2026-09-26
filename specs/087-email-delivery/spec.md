# Feature Specification: Email delivery

**Feature Branch**: `087-email-delivery` | **Created**: 2026-09-27 | **Issue**: #175 (closes it)

## Why

An email that could not be delivered ends `Failed` in `outgoing_emails`, with its last error (specs/060). That
happens after 12 attempts with the mail server down, or at once for an unknown recipient or missing words.
Nothing read those rows. The person never got their confirmation, reset link or lock notice, and nobody but a
database user could find out why.

## Requirements

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

## Decisions

- **Sent emails are listed too.** The issue asked whether they should be, since the list records who bought what.
  An administrator already reads every order, so this discloses nothing new, and "did the confirmation go out?" is
  the first support question.
- **The retried email is in the audit log, not the email's own history.** `outgoing_emails` keeps one current
  state. The attempts and the error it had are the audit entry's "before".

## Out of scope

- Alerting anyone when an email fails.
