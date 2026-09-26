# Research: An administrator edits the emails

> Written on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

D1-D4 are the decisions the original plan recorded inline, carried here unchanged in substance. D5-D8 are the
issue's open questions, answered in the spec. D9-D14 are decisions the code and the pull request record. Where the
record names no rejected alternative, this file says "not recorded" rather than supplying one.

---

## D1 - Versions, not updates

**Decision**: `email_template_versions` is append-only. Every save adds a row with the next number; a reset and a
restore add a row too. The current words are the highest version.

**Rationale**: A bad edit is undone by restoring an earlier version, and the history is the audit's companion.
Because nothing is rewritten, anything can be undone, including an undo.

**Alternatives considered**:

- **Update one row per template and language in place.** Rejected: an update-in-place would need the audit log to
  reconstruct the previous words, and undoing an edit would itself be an edit with nothing to go back to.
- **Delete versions on reset.** Not recorded as considered; the design adds a default version instead, so a reset
  is itself undoable.

---

## D2 - Optimistic concurrency with the version number

**Decision**: The editor sends the version it opened (`expectedVersion`). The handler refuses with 409 if it is not
the current version, then inserts `expectedVersion + 1` with `INSERT ... ON CONFLICT ("Template", "Language",
"Version") DO NOTHING`; zero rows inserted is the same 409.

**Rationale**: The editor sends the version it opened. The unique index turns two saves at once into one version
and one 409, and neither administrator loses their text silently. The two guards cover different windows: the
version check stops a stale editor and a made-up future number; the index stops the saves that race past the
check. The mutation checks showed each alone was hidden by the other - removing either left the tests green -
until two tests pinned them separately (the store gives a number once; a version from the future is a 409).

**Alternatives considered**:

- **Last write wins.** Rejected: one administrator's words would silently replace another's.
- **A row lock, or `xmin`, on a "current" row.** Not recorded; with D1 there is no current row to lock, and the
  unique index already decides.
- **EF `Add` and catching the unique violation.** Not recorded as considered. The raw `INSERT ... ON CONFLICT`
  also stages the audit entry only for the winner, so the loser writes nothing at all.

---

## D3 - Sanitise the template, escape the values

**Decision**: The template's HTML is sanitised with an allow-list on save, on restore, on preview and test, and
again on every send (`EmailComposer`). Values are HTML-escaped as they are filled in, in text and in an `href`
alike.

**Rationale**: The template is sanitised on save and again when sent. Values (a name) are escaped as they are
filled in, so a name like `<b>` shows as text. A link's value is the storefront's own URL. Sanitising again on
every send means a sanitiser fixed later protects versions saved before the fix (docs/project/decisions.md, row
58); a restore re-checks by today's rules for the same reason.

**Alternatives considered**:

- **Sanitise on save only.** Rejected for the reason above: a stored version would be trusted for ever by the
  rules of the day it was saved.
- **Trust the editor.** Rejected (spec, Decisions): the server's allow-list restricts what is kept, whatever the
  client produced - an administrator is trusted, what they paste from elsewhere is not.

---

## D4 - The test email is sent at once, not queued

**Decision**: `POST .../test` calls `IEmailTransport.SendAsync` directly with the subject prefixed `[Test] `; a
failure is a `DependencyUnavailableException`, a 503.

**Rationale**: It is the administrator checking their own draft, so a mail server that is down is a 503 they can see
now, not a retry an hour later.

**Alternatives considered**:

- **Queue it through `outgoing_emails` like every other email.** Rejected: the dispatcher's backoff (1, 2, 4 ...
  minutes) would turn "is my draft right?" into a wait with no answer.

---

## D5 - The security emails are editable, but `{link}` is locked in

**Decision**: `PasswordReset` and `EmailConfirmation` are editable by an administrator like `OrderPaid`, and a save
without `{link}` in the body is a 400 (`EmailTemplates.RequiredOf`).

**Rationale**: Administrators already hold everything, and the refusal protects the one thing that matters: a
reset or confirmation email without its link cannot do what it is for.

**Alternatives considered**:

- **A narrower role for the security emails.** Rejected: it would be a role nobody has.
- **Leave the security emails uneditable.** Not recorded as considered beyond the issue's open question.

---

## D6 - A language without an edit uses its own built-in words

**Decision**: The words for a template are looked up per language; a supported language with no edit renders its
own built-in words.

**Rationale**: A Vietnamese reader never gets an English email because only the English one was edited. (A
language the shop does not write in reads the default language, Vietnamese - its edit if there is one, as the
code of `EmailComposer` does.)

**Alternatives considered**:

- **Fall back to the other language's edit.** Rejected for the reason above.

---

## D7 - TipTap, restricted by the server's allow-list

**Decision**: The storefront's editor is TipTap with StarterKit (bold, italic, underline, strike, link, lists, a
quote, headings 2-3; no code or code block), limited to what the server keeps, and a link may point at a
placeholder such as `{link}`.

**Rationale**: What the administrator sees is what is sent, because the editor offers only what the allow-list
keeps; but the allow-list, not the editor, is what is trusted.

**Alternatives considered**: not recorded (no other editor is named in the issue, the pull request or the docs).

---

## D8 - Ganss.Xss on the server; the preview is the server's rendering in a sandboxed `iframe`

**Decision**: `AllowListHtmlSanitizer` wraps `HtmlSanitizer` (Ganss.Xss 9.2.1039) with its allow-lists cleared and
refilled: tags `p br strong b em i u s a h1 h2 h3 ul ol li blockquote hr`; `href` the only attribute and the only
URI attribute; schemes `http`, `https`, `mailto`; no CSS properties, at-rules or classes. The storefront never
renders a draft itself: the preview is the server's HTML in `<iframe sandbox="" srcDoc=...>`.

**Rationale**: One sanitiser, on the side that sends. The preview shows exactly what would be sent, and the empty
`sandbox` means nothing in it can run or reach the page.

**Alternatives considered**: not recorded.

---

## D9 - The built-in words stay in code, their HTML derived from the plain text

**Decision**: `EmailTemplates` keeps its plain-text subjects and bodies; `EmailHtml.FromPlainText` turns a body into
a paragraph per blank-line block, a `<br>` per line, and `{link}` into `<a href="{link}">{link}</a>`. "Reset to
default" adds an `IsDefault` version, whose subject and body are null.

**Rationale**: An unedited template sends the same words as before, now as HTML plus text, with no data migration
and no seed rows; an earlier image, which knows nothing of the table, sends the same words.

**Alternatives considered**: seeding the defaults as version 1 rows - not recorded as considered.

---

## D10 - Escape five characters with an own encoder, not `WebUtility.HtmlEncode`

**Decision**: `EmailHtml.Encode` replaces `&`, `<`, `>`, `"` and `'` and nothing else.

**Rationale**: `WebUtility.HtmlEncode` also turns letters like "é" into numeric entities, which then show up in the
editor - and most of this shop's words are Vietnamese.

**Alternatives considered**:

- **`WebUtility.HtmlEncode`.** Rejected for the reason above.

---

## D11 - `multipart/alternative`, text first, HTML in a minimal inline-styled document

**Decision**: `IEmailTransport.SendAsync(to, subject, text, html)`; `SmtpEmailTransport` adds the `text/plain` view
first and the `text/html` view second, the HTML wrapped in a document whose styles are inline. The text is derived
from the rendered HTML (`EmailHtml.ToText`): paragraphs and breaks kept, a list item as a dash, a link as its
address or "words (address)".

**Rationale**: A mail client shows the last part it can render, so HTML where it can and the text where it cannot.
An email's styles have to be inline.

**Alternatives considered**:

- **HTML only.** Not recorded as considered; FR-006 required the text alternative from the start.

---

## D12 - No images

**Decision**: `img` is not on the allow-list; images are removed on save.

**Rationale**: A remote image in an email is also a read receipt. Recorded in the pull request and the docs as a
known limit: no logo.

**Alternatives considered**: not recorded.

---

## D13 - Administrators only, not moderators

**Decision**: `[Authorize(Roles = "Admin")]` on the whole controller, not `StaffRoles.Staff`; the menu item is
`adminOnly` and the route `RequireRole role={['Admin']}`.

**Rationale**: These reach every customer's inbox, and two of them carry the link that resets a password.

**Alternatives considered**:

- **Staff (Admin or Moderator), like moderation.** Rejected for the reason above.

---

## D14 - The editor page is lazy-loaded

**Decision**: `AdminEmailsPage` is loaded with `React.lazy` behind a `Suspense` in `routes/index.tsx`.

**Rationale**: No shopper downloads ProseMirror to look at a camera. At merge the main bundle stayed at 1.09 MB and
TipTap was a 401 kB chunk only an administrator downloads.

**Alternatives considered**:

- **Bundle it with the rest.** Rejected for the reason above.
