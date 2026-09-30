# Data model: Administrators are told when an email fails for good

## Identity

| Table | Change |
| :-- | :-- |
| `outgoing_emails` | `FailureAlertedAt timestamp with time zone NULL` - migration `AddEmailFailureAlert`. Expand only. |

Written by: the dispatcher's claim (research D2) - set on every failed, uncounted email when a notice goes out; the
retry (specs/087) - cleared with the rest of the failure. Never read by an older image.

## Messages

`UserNotificationRequested` (existing) with kind `EmailsFailed`, data `{ "failed": "<count>" }`, link
`/admin/email-delivery` - one per administrator.

## States

An email: `Pending` → `Sent` | `Failed` (unchanged). A failed email: uncounted → counted (`FailureAlertedAt`) → back to
`Pending` and uncounted on a retry.
