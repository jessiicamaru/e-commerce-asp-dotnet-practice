# Feature Specification: An administrator edits the emails

> Completed on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Feature Branch**: `077-email-templates` | **Created**: 2026-09-26 | **Issue**: #150 (the email half; the
notification half is specs/078, which closes it)

**Status**: Merged (#161, 2026-09-26)

**Input**: Issue #150, the email half: "an administrator rewords what the shop sends" - the emails of
[specs/060](../060-email/), [specs/061](../061-password-reset/) and [specs/063](../063-email-confirmation/) were
fixed strings in code.

## Why

The three emails the shop sends are fixed strings in C#: the order confirmation, the password reset and the address
confirmation, each in Vietnamese and English. They go out as plain text. Rewording one, fixing a Vietnamese typo,
adding the shop's name or giving it a layout needs a developer and a release.

## User Scenarios & Testing *(mandatory)*

### US1 - Edit an email (P1)

An administrator opens **Emails** in the admin console, picks a template and a language, and edits the subject and
a rich-text body.
- Placeholders such as `{name}` and `{link}` are inserted from a list rather than typed.
- After saving, the next email of that kind goes out with the new words, as HTML with a plain-text alternative.
- Saving a placeholder the template does not carry is refused, naming it.
- The password reset and address confirmation must keep `{link}`: without it the email is useless.

**Why this priority**: It is the feature. Everything else - preview, history, the role check - exists to make this
one action safe.

**Independent Test**: Save new English words for `OrderPaid`, then let an English order settle `Paid` and read
the email in Mailpit: its subject and body are the saved words, filled in, as HTML with a text part beside it; the
Vietnamese confirmation of another order is unchanged.

**Acceptance Scenarios**:

1. **Given** `OrderPaid`/`en` has never been edited, **When** an administrator saves a subject and body on top of
   version 0, **Then** version 1 is stored, `isDefault` is false, and the next English order confirmation says
   those words.
2. **Given** that edit, **When** a Vietnamese order settles, **Then** its email is the Vietnamese built-in words,
   not the English edit.
3. **Given** a draft whose subject says `{discount}` and whose body says `{voucher}`, **When** it is saved,
   **Then** the answer is 400 with one message on `Subject` naming `{discount}` and one on `BodyHtml` naming
   `{voucher}`, and nothing is stored.
4. **Given** a password-reset or address-confirmation draft without `{link}`, **When** it is saved, **Then** the
   answer is 400 saying the email must keep `{link}`.
5. **Given** a body carrying `onclick`, `style`, `<script>`, a `javascript:` link, an `<img>` and an `<iframe>`,
   **When** it is saved, **Then** what is stored has none of them, and a recipient named `<b>Mai</b>` reads that
   text rather than bold markup.

---

### US2 - Preview and test (P1)

The administrator sees the draft rendered with sample data, and can send it as a test to their own address
(Mailpit in development) before saving.

**Why this priority**: An email cannot be taken back once sent. Seeing it as a recipient would, before anybody
receives it, is what makes editing live words acceptable.

**Independent Test**: Preview a draft containing a `<script>`; the answer has the draft filled with made-up data,
without the script, plus its plain text. Send it as a test; one email with the subject `[Test] ...` reaches the
administrator's own address and nothing is saved.

**Acceptance Scenarios**:

1. **Given** a draft, **When** it is previewed, **Then** the answer is the sanitised draft filled with sample data
   (never a real order or token) as subject, HTML and text, and nothing is stored.
2. **Given** a draft, **When** a test is sent, **Then** exactly one email, subject prefixed `[Test] `, goes to the
   address of the account the caller's token names, and the template's version is unchanged.
3. **Given** the mail server is down, **When** a test is sent, **Then** the answer is 503 at once, not a retry
   later.
4. **Given** a preview, **When** the storefront shows it, **Then** it is the server's rendering inside a sandboxed
   `iframe` in which nothing can run.

---

### US3 - Undo (P2)

Every save is a new version.
- **Reset to default** returns to the built-in words.
- Any earlier version can be restored, which makes a new version and never rewrites history.
- Every save, reset and restore is in the audit log with its before and after.

**Why this priority**: A mistake has to be recoverable without a developer, but the feature is usable - carefully -
without it, so it comes after editing and previewing.

**Independent Test**: Save version 1, reset (version 2, the built-in words), restore version 1 (version 3, the
saved words again); the history lists 3, 2, 1 and the audit log has a `System` entry for each with before and
after.

**Acceptance Scenarios**:

1. **Given** version 1 is an edit, **When** the administrator resets on top of version 1, **Then** version 2 is
   stored with `isDefault` true and the answer carries the built-in subject.
2. **Given** the current version already uses the built-in words, **When** a reset is asked for, **Then** the
   answer is 409 and nothing is stored.
3. **Given** versions 1 and 2, **When** version 1 is restored on top of version 2, **Then** version 3 carries
   version 1's words, sanitised and checked again by today's rules, and versions 1 and 2 are untouched.
4. **Given** any save, reset or restore, **When** it is stored, **Then** an `EmailTemplateSaved`,
   `EmailTemplateReset` or `EmailTemplateRestored` audit entry with subject `{template}/{language}` and the words
   before and after commits with it.
5. **Given** an editor opened at version 1, **When** somebody else saves version 2 and the first editor then
   saves, **Then** the first save is 409 and neither administrator's words are lost silently.

---

### US4 - Only administrators (P1)

A moderator gets 403 on every template endpoint, and so does anybody else who is not an administrator.

**Why this priority**: The emails reach every customer's inbox, and two of them carry the link that resets a
password. A wrong role here is a phishing channel.

**Independent Test**: `GET /api/email-templates` with a moderator's token through the gateway is 403; with no
token, 401.

**Acceptance Scenarios**:

1. **Given** a moderator's token, **When** any `/api/email-templates` endpoint is called, **Then** it is 403.
2. **Given** no token, **When** any of them is called, **Then** it is 401.
3. **Given** the storefront, **When** a moderator opens the admin console, **Then** there is no **Emails** item and
   `/admin/emails` does not draw - for drawing only; the server refuses on its own.

---

### Edge Cases

- **Two administrators save at once on the same version.** Both ask for N + 1; the unique `(Template, Language,
  Version)` lets exactly one insert, and the other gets the same 409 as a stale editor. Eight saves raced in the
  tests produce one version.
- **A version number from the future.** An `expectedVersion` above the current one is a 409 - it would leave a gap
  in the history.
- **An unknown template or language.** `Newsletter` or `fr` is a 404 on every endpoint.
- **Restoring a version that does not exist.** 404, `Version N of this email was not found.`
- **Restoring an old version the rules now refuse.** The restored words are sanitised and their placeholders
  checked again, so a restore can be a 400 even though the version was once accepted.
- **Restoring a reset.** Restoring an `IsDefault` version adds another default version.
- **An email in a language the shop does not write in.** It reads the default language (Vietnamese) - that
  language's edit if there is one.
- **A `{link}` placeholder in an `href`.** It survives the sanitiser as a relative address; the value filled in is
  the storefront's own URL, HTML-escaped.
- **A value containing HTML.** A name like `<b>Mai</b>` is escaped wherever it is filled in, in text and in an
  `href`. The subject is a header, not HTML: it is filled unescaped and kept to one line.
- **An empty body after sanitising.** A body that was only a `<script>` is refused as empty.
- **A body over 20,000 characters or a subject over 200.** Refused by the validators before anything else.
- **Older images during a rollback.** They ignore the new table and send the built-in words as plain text.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-008** Reset and restore MUST each add a version on top of the one the editor opened; no version is ever
  updated or deleted. A reset of an email that already uses the built-in words MUST be refused (409).
- **FR-009** Every save, reset and restore MUST record an audit entry (category `System`) with the words before and
  after, committed in the same transaction as the version - or not at all.
- **FR-010** A preview and a test MUST fill the draft with made-up data - never a real order or token - and MUST
  NOT store anything. A test MUST go only to the address of the account named by the caller's token, and a mail
  server that refuses it MUST be reported at once (503), not queued.
- **FR-011** A placeholder refusal MUST name the placeholder and list what the email can use, on the field
  (`Subject` or `BodyHtml`) it is in; every refusal of one draft is reported together.
- **FR-012** An unknown template or language MUST be a 404, and the storefront's editor code MUST NOT be part of
  what a shopper downloads (the page is lazy-loaded).

### Key Entities

- **Email template version**: one saved state of one email's words in one language - an administrator's edit, a
  reset to the built-in words, or an earlier version restored. Numbered 1, 2, 3 ... per template and language;
  the highest is what is sent. Append-only.
- **Built-in words**: the subject and plain-text body in code for each template and language; the default, and
  what a reset returns to. Their HTML is derived, not stored.
- **Placeholder**: a `{name}` in a subject or body that the email fills when sent. Each template has a fixed list
  it may use and a list it must keep.
- **Rendered email**: a subject, an HTML body and its plain-text alternative, filled for one recipient.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator changes the words of any of the 3 emails in either of the 2 languages without a
  release: the next email of that kind carries them. Verified live in Mailpit at merge (subject "Welcome, Linh -
  confirm your address").
- **SC-002**: 0 stored versions contain a script, an event handler, a `style` attribute, an image, a frame or a
  non-`http`/`https`/`mailto` link, and the sanitiser runs again on every send.
- **SC-003**: Of N saves racing on one version, exactly 1 succeeds and N - 1 get 409 (8 in the test).
- **SC-004**: 100% of saves, resets and restores appear in the audit log with before and after.
- **SC-005**: A moderator gets 403 on 100% of the 7 endpoints (Bruno checks the list endpoint).
- **SC-006**: A shopper's main bundle does not grow by the editor: at merge the main bundle was 1.09 MB and TipTap a
  separate 401 kB chunk only an administrator downloads.

## Assumptions

- Administrators are trusted to write the words; what they paste from elsewhere is not, which is why the
  allow-list runs on the server whatever the editor produced.
- Three templates and two languages are the whole set; adding a template is a code change (the placeholders a
  template can fill are code).
- The email pipeline of [specs/060](../060-email/) - `EmailRequested`, `outgoing_emails` and the dispatcher -
  is unchanged; this feature changes only what the dispatcher renders and how the transport sends it.
- The storefront URL filled into `{link}` is the service's own setting (`STOREFRONT_URL`), never input.

## Decisions (the issue's open questions)

- **The security emails are editable, but `{link}` is locked in.** A narrower role would be a role nobody has.
  Administrators already hold everything, and the refusal protects the one thing that matters.
- **A language without an edit uses its own built-in words.** It does not use the other language's edit: a
  Vietnamese reader never gets an English email because only the English one was edited.
- **TipTap** (StarterKit: bold, italic, underline, link, lists, headings), restricted by the server's allow-list,
  not by trusting the editor.
- **Sanitiser**: `HtmlSanitizer` (Ganss.Xss) on the server. The storefront's preview is the server's rendering,
  shown in a sandboxed `iframe`.

More decisions, with their rejected alternatives, are in [research.md](./research.md).

## Out of scope

Notification wording (specs/078), new templates, attachments, and per-shop templates. Also images (a remote image
in an email is a read receipt), a moderator role for emails, and a screen of failed emails.
