# Implementation Plan: An administrator edits the emails

**Branch**: `077-email-templates` | **Spec**: [spec.md](spec.md) | **Issue**: #150 (the email half)

## Design (Identity)

- `EmailTemplateVersion` and the `email_template_versions` table (the migration `AddEmailTemplateVersions`), with
  a CHECK: a version is either the default or has both a subject and a body.
- `EmailTemplates` keeps the built-in words as the defaults, and now also exposes:
  - `Templates`, `Languages`, `PlaceholdersOf` and `RequiredOf`;
  - `Default(t, lang)` as HTML (`EmailHtml.FromPlainText`);
  - `SampleData`;
  - `Render(..., edited)`, which returns `RenderedEmail(Subject, Html, Text)`.
- `EmailHtml` is pure code: the placeholders a text uses, plain text to HTML, filling values (HTML-escaped with a
  five-character encoder, not `WebUtility`, which also turns letters like "é" into entities), and HTML to text.
- `IHtmlSanitizer` is implemented by `AllowListHtmlSanitizer` (Ganss.Xss): an allow-list of tags, `href` as the only
  attribute, and `http`, `https` and `mailto` as the only schemes. It is used on save, on restore and on every send.
- `EmailComposer` (Application) supplies the current version's words, sanitised again, or the built-in ones.
  `DispatchEmailsCommandHandler` sends through it.
- `IEmailTransport.SendAsync(to, subject, text, html)`: `SmtpEmailTransport` sends `multipart/alternative`, with
  the HTML wrapped in a minimal inline-styled document.
- `EmailTemplateStore.TryAddAsync` does `INSERT ... ON CONFLICT DO NOTHING` on the unique `(Template, Language,
  Version)`. Only if the row is inserted is the audit entry staged and saved, in one transaction.
- `EmailTemplatesController`, at `api/email-templates` and `Admin` only, and a gateway route to Identity.

## Storefront

- `services/email-template` and `hooks/email-template`.
- `components/shared/rich-text-editor` is TipTap with StarterKit and Link. Its toolbar has bold, italic, underline,
  strike, H2, lists, quote, link and undo/redo. A placeholder list inserts `{name}` at the cursor. The link
  button accepts an address or a placeholder such as `{link}`.
- `pages/admin-emails` (`/admin/emails`, under the admin layout with `adminOnly`):
  - a template × language picker;
  - the subject and body editors;
  - the placeholders, with the required ones marked;
  - save, preview (the server's rendering in a sandboxed `iframe`), send a test, reset, and the versions, each
    with restore.
- The words go in `admin` (`emails.*`), in `vi` and `en`.
- In tests the editor is replaced by a textarea (`vi.mock`), because ProseMirror needs layout APIs jsdom lacks. The
  editor's own test covers only what jsdom can.

## Research

- **D1 - versions, not updates.** A bad edit is undone by restoring an earlier version, and the history is the
  audit's companion. An update-in-place would need the audit log to reconstruct the previous words.
- **D2 - optimistic concurrency with the version number.** The editor sends the version it opened. The unique index
  turns two saves at once into one version and one 409, and neither administrator loses their text silently.
- **D3 - sanitise the template, escape the values.** The template is sanitised on save and again when sent. Values
  (a name) are escaped as they are filled in, so a name like `<b>` shows as text. A link's value is the
  storefront's own URL.
- **D4 - the test email is sent at once, not queued.** It is the administrator checking their own draft, so a
  mail server that is down is a 503 they can see now, not a retry an hour later.
