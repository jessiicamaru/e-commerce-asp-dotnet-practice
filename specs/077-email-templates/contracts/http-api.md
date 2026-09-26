# HTTP Contract: An administrator edits the emails

> Written on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Feature**: [spec.md](../spec.md) | **Data**: [data-model.md](../data-model.md)

Seven endpoints on Identity's `EmailTemplatesController`, all **`Admin`** (`[Authorize(Roles = "Admin")]` on the
class). A moderator, a seller or a customer gets **403**; no token, **401**.

**Base**: Identity at `http://localhost:5056`, reached through the gateway at `http://localhost:5000` by two routes
to `identity-cluster`: `email-templates-root-route` (`/api/email-templates`) and `email-templates-route`
(`/api/email-templates/{**catch-all}`).

`{template}` is `OrderPaid`, `PasswordReset` or `EmailConfirmation`; `{language}` is `vi` or `en`. Anything else is
**404** `No such email template.` on every endpoint. JSON is camelCase.

Errors follow the project's RFC 7807 shape through `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
A 400 carries an `errors` object keyed by field (`Subject`, `BodyHtml`, `ExpectedVersion`).

---

## Shapes

**`EmailTemplateResponse`** - one email in one language as it currently reads:

```json
{
  "template": "PasswordReset",
  "language": "en",
  "subject": "Reset your password",
  "bodyHtml": "<p>Hi {name},</p><p>...</p><p><a href=\"{link}\">{link}</a></p>...",
  "isDefault": true,
  "version": 0,
  "updatedAt": null,
  "updatedBy": null,
  "placeholders": ["name", "link"],
  "required": ["link"]
}
```

`version` 0 means nobody ever saved one. When `isDefault`, `subject` and `bodyHtml` are the built-in words as HTML.
`placeholders` is what the email may use; `required` what it may not lose.

**`EmailTemplateVersionResponse`**: `version`, `isDefault`, `subject`, `bodyHtml`, `createdAt`, `createdBy`. A
default version is shown with the built-in words.

**`EmailPreviewResponse`**: `subject`, `html`, `text` - the draft filled with made-up data.

---

## `GET /api/email-templates`

Every template in every language (6 at merge), current, in the console's order. **200** with
`EmailTemplateResponse[]`.

## `GET /api/email-templates/{template}/{language}/versions`

The saved versions, newest first. **200** with `EmailTemplateVersionResponse[]` (empty when never edited). **404**
for an unknown template or language.

## `PUT /api/email-templates/{template}/{language}`

Save new words as a new version.

```json
{ "subject": "Thank you - order {order} is paid", "bodyHtml": "<p>Hi {name},</p>...", "expectedVersion": 0 }
```

| Status | When |
| :--- | :--- |
| **200** | Stored as version `expectedVersion + 1`; body is the new `EmailTemplateResponse` (`isDefault: false`) |
| **400** | Subject or body empty, subject over 200 or body over 20,000 characters, `expectedVersion` negative; after sanitising: a placeholder the email cannot fill (named, e.g. `{discount} is not something this email can fill in. It can use: {name}, {order}, {total}, {link}.`), a required placeholder missing (`This email must keep {link} - ...`), or a body with no text left |
| **404** | Unknown template or language |
| **409** | `expectedVersion` is not the current version, or another save took that number first: `Somebody changed this email since you opened it. Reload it and make your change again.` |

The body is sanitised before it is checked and stored: allowed tags `p br strong b em i u s a h1 h2 h3 ul ol li
blockquote hr`, `href` the only attribute, `http`/`https`/`mailto` the only schemes (a `{link}` placeholder survives
as a relative address). The subject's line breaks become spaces.

## `POST /api/email-templates/{template}/{language}/reset`

Back to the built-in words, as a new `IsDefault` version. Body `{ "expectedVersion": 1 }`.

**200** with the new `EmailTemplateResponse` (`isDefault: true`, the built-in subject). **404** unknown template
or language. **409** already the built-in words (`This email already uses the built-in words.`), or stale as above.

## `POST /api/email-templates/{template}/{language}/versions/{version}/restore`

An earlier version's words as a new version. `{version}` is an integer route constraint. Body
`{ "expectedVersion": 2 }`.

**200** with the new `EmailTemplateResponse`. **400** if the restored words fail today's placeholder rules (they
are sanitised and checked again). **404** unknown template or language, or `Version N of this email was not
found.` **409** stale as above.

## `POST /api/email-templates/{template}/{language}/preview`

A draft rendered with made-up data - nothing saved, nothing sent. Body `{ "subject": "...", "bodyHtml": "..." }`.

**200** with `EmailPreviewResponse`: the sanitised draft filled with `SampleData` (an order id `01a0dd2b-sample`,
1,250,000 VND, or the token `sample-token`) and the caller's `given_name` (or "Mai"). **400** / **404** as for a
save.

## `POST /api/email-templates/{template}/{language}/test`

The same rendering, sent at once to the address of the account the caller's token names, subject prefixed
`[Test] `. Nothing saved. Body as for a preview.

**200** with the `EmailPreviewResponse` that was sent. **400** / **404** as for a save. **401** if the token names no
account. **503** if the mail server did not take it (`The mail server did not take the test email: ...`) - it is
not queued or retried.

---

## Audit

Save, reset and restore each record one `AuditEntryRecorded` (specs/041) in the same transaction as the version:
category `System`, action `EmailTemplateSaved` / `EmailTemplateReset` / `EmailTemplateRestored`, subject type
`EmailTemplate`, subject id `{template}/{language}`, before and after `{ Subject, BodyHtml, IsDefault }`. Read them
at `GET /api/audit?action=EmailTemplateSaved` (Admin).

## Messaging

**No message contract changed**, so there is no `messages.md`. The feature relies on two existing ones:

- `EmailRequested` ([specs/060](../../060-email/)), published by Order through `IEmailSender` and queued by
  Identity's `QueueEmailConsumer` into `outgoing_emails`. Unchanged: it carries a template name, data and a
  language, never words; the words are chosen at send time by `EmailComposer`.
- `AuditEntryRecorded` (specs/041), published through Identity's outbox, as above.

## What reaches the mail server

Not an HTTP contract, but an external interface that changed: every email - the dispatcher's and a test - is one
SMTP message with `multipart/alternative` content, a `text/plain` part first and a `text/html` part second, both
UTF-8, the subject UTF-8. The HTML part is the rendered body inside a minimal document with inline styles. The
internal port is `IEmailTransport.SendAsync(to, subject, text, html)`, which replaced `SendAsync(to, subject,
body)`.
