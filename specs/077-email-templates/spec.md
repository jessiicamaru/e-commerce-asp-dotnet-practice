# Feature Specification: An administrator edits the emails

**Feature Branch**: `077-email-templates` | **Created**: 2026-09-26 | **Issue**: #150 (the email half; the
notification half is specs/078, which closes it)

## Why

The three emails the shop sends are fixed strings in C#: the order confirmation, the password reset and the address
confirmation, each in Vietnamese and English. They go out as plain text. Rewording one, fixing a Vietnamese typo,
adding the shop's name or giving it a layout needs a developer and a release.

## User Scenarios

### US1 - Edit an email (P1)

An administrator opens **Emails** in the admin console, picks a template and a language, and edits the subject and
a rich-text body.
- Placeholders such as `{name}` and `{link}` are inserted from a list rather than typed.
- After saving, the next email of that kind goes out with the new words, as HTML with a plain-text alternative.
- Saving a placeholder the template does not carry is refused, naming it.
- The password reset and address confirmation must keep `{link}`: without it the email is useless.

### US2 - Preview and test (P1)

The administrator sees the draft rendered with sample data, and can send it as a test to their own address
(Mailpit in development) before saving.

### US3 - Undo (P2)

Every save is a new version.
- **Reset to default** returns to the built-in words.
- Any earlier version can be restored, which makes a new version and never rewrites history.
- Every save, reset and restore is in the audit log with its before and after.

### US4 - Only administrators (P1)

A moderator gets 403 on every template endpoint, and so does anybody else who is not an administrator.

## Requirements

- **FR-001** `email_template_versions (Id, Template, Language, Version, Subject, BodyHtml, IsDefault, CreatedAt,
  CreatedBy)`, unique on `(Template, Language, Version)`.
  - The current version is the highest one.
  - An `IsDefault` version, or no version at all, means the built-in words.
  - A save names the version it edited. A stale one is a 409, and the unique index is what makes two
    simultaneous saves produce one version.
- **FR-002** The built-in words stay in code as the defaults. Their HTML form is derived from the plain text:
  paragraphs, line breaks, and `{link}` as a link. An unedited template sends the same words as before, now as
  HTML plus text.
- **FR-003** HTML is sanitised with an allow-list on save and again on render. No scripts, no event handlers, no
  `style` or `iframe`, and links only to `http`, `https` or `mailto`.
  - Values filled in at send time (a name, a link) are HTML-escaped before they enter the HTML.
- **FR-004** Placeholders per template:
  - `OrderPaid`: `name`, `order`, `total`, `link`;
  - `PasswordReset`: `name`, `link` (`link` required);
  - `EmailConfirmation`: `name`, `link` (`link` required).
- **FR-005** The endpoints, all `Admin`:

  | Method | Path | What |
  | :-- | :-- | :-- |
  | `GET` | `/api/email-templates` | Every template × language, current. |
  | `GET` | `/api/email-templates/{t}/{lang}/versions` | The history. |
  | `PUT` | `/api/email-templates/{t}/{lang}` | Save a new version. |
  | `POST` | `/api/email-templates/{t}/{lang}/reset` | Back to the built-in words. |
  | `POST` | `/api/email-templates/{t}/{lang}/versions/{v}/restore` | Restore an earlier version. |
  | `POST` | `/api/email-templates/{t}/{lang}/preview` | Render a draft with sample data. |
  | `POST` | `/api/email-templates/{t}/{lang}/test` | Send a draft to the caller. |

- **FR-006** The emails are sent as `multipart/alternative`: HTML, plus text derived from it.
- **FR-007** Storefront: `/admin/emails` (administrators only), with a rich-text editor (TipTap), the placeholder
  list, the preview, the test send, reset and the versions. Vitest tests.

## Decisions (the issue's open questions)

- **The security emails are editable, but `{link}` is locked in.** A narrower role would be a role nobody has.
  Administrators already hold everything, and the refusal protects the one thing that matters.
- **A language without an edit uses its own built-in words.** It does not use the other language's edit: a
  Vietnamese reader never gets an English email because only the English one was edited.
- **TipTap** (StarterKit: bold, italic, underline, link, lists, headings), restricted by the server's allow-list,
  not by trusting the editor.
- **Sanitiser**: `HtmlSanitizer` (Ganss.Xss) on the server. The storefront's preview is the server's rendering,
  shown in a sandboxed `iframe`.

## Out of scope

Notification wording (specs/078), new templates, attachments, and per-shop templates.
