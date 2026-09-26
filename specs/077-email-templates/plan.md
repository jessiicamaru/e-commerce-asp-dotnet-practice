# Implementation Plan: An administrator edits the emails

> Completed on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Branch**: `077-email-templates` | **Spec**: [spec.md](spec.md) | **Issue**: #150 (the email half)

## Summary

Identity already sends three emails from words fixed in code ([specs/060](../060-email/),
[061](../061-password-reset/), [063](../063-email-confirmation/)). This feature lets an administrator replace
those words per template and language, without a release, and sends every email as HTML with a plain-text
alternative.

The approach: an append-only table of versions in Identity, the one service that sends email; the code's words
stay as the defaults; an allow-list sanitiser runs when words are saved **and** again every time they are sent;
values are HTML-escaped as they are filled in; placeholders are checked by name. The storefront gets
`/admin/emails`, a lazy-loaded TipTap editor with a server-rendered preview. The decisions and their rejected
alternatives are in [research.md](./research.md); the table is in [data-model.md](./data-model.md); the endpoints
are in [contracts/http-api.md](./contracts/http-api.md).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (Identity); TypeScript with React 19 (storefront)

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, MassTransit 8.3.6 (the
EF outbox, for the audit entry), **HtmlSanitizer 9.2.1039 (Ganss.Xss) - new**, `System.Net.Mail`;
`Ecommerce.Shared` (`IAuditTrail`, exceptions, `ICurrentUser`). Storefront: **`@tiptap/react`,
`@tiptap/starter-kit`, `@tiptap/pm` ^3.31.3 - new**, TanStack Query 5, react-i18next

**Storage**: PostgreSQL 16, `ecommerce_identity_db` (host port 5435): one new table, `email_template_versions`

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Identity.Tests`, 13 new in `EmailTemplateTests`, 163 in
all at merge); Vitest with jsdom (435 at merge); the Bruno collection through the gateway; a live check in
Mailpit

**Target Platform**: the Identity service (REST 5056) behind the gateway (5000); the storefront

**Project Type**: an addition to an existing Clean Architecture service, plus a storefront page

**Performance Goals**: none stated. Rendering reads the current version once per email sent, by the unique index

**Constraints**: nothing that could run in an inbox may be stored or sent; a stale editor must never overwrite
somebody else's words silently; the save and its audit entry commit together; an earlier image must still run
against the migrated schema; no shopper downloads the editor

**Scale/Scope**: 3 templates × 2 languages; one administrator editing at a time in practice

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

Added on 2026-09-27 from the code: the handlers are one class, `EmailTemplateHandlers`, in
`Application/Email/EmailTemplateEditing.cs`, beside the commands, queries, validators, `IEmailTemplateStore`,
`IHtmlSanitizer` and `EmailComposer`. A save first compares `expectedVersion` with the current version (409 when
they differ), then inserts `expectedVersion + 1`; losing the insert is the same 409. The transaction runs inside
`CreateExecutionStrategy().ExecuteAsync`, as every hand-opened transaction in this repository must. The gateway
has two routes to `identity-cluster`, `/api/email-templates` and `/api/email-templates/{**catch-all}`.

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

Added on 2026-09-27 from the code and the pull request: the page is **lazy-loaded** (`React.lazy` in
`routes/index.tsx`, behind `RequireRole role={['Admin']}`), so the main bundle stayed at 1.09 MB and TipTap is a
401 kB chunk only an administrator downloads. The editor is remounted by a `key` of template, language and
version, so a save, reset or restore starts from the server's words. The StarterKit is configured without `code`
and `codeBlock`, with headings 2 and 3, and a link may point at a placeholder.

## Research

Carried into [research.md](./research.md) as D1-D4, with the further decisions the code and pull request record
(D5-D14).

- **D1 - versions, not updates.** A bad edit is undone by restoring an earlier version, and the history is the
  audit's companion. An update-in-place would need the audit log to reconstruct the previous words.
- **D2 - optimistic concurrency with the version number.** The editor sends the version it opened. The unique index
  turns two saves at once into one version and one 409, and neither administrator loses their text silently.
- **D3 - sanitise the template, escape the values.** The template is sanitised on save and again when sent. Values
  (a name) are escaped as they are filled in, so a name like `<b>` shows as text. A link's value is the
  storefront's own URL.
- **D4 - the test email is sent at once, not queued.** It is the administrator checking their own draft, so a
  mail server that is down is a 503 they can see now, not a retry an hour later.

## Constitution Check

*GATE: evaluated after the fact against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The
original plan had no Constitution Check; this section was added on 2026-09-27.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The words live in Identity, the one service that already knows addresses and sends email (`outgoing_emails`, specs/060); no other service reads them. Order still asks for an email through the unchanged `EmailRequested`, and knows nothing of the words. The storefront reaches Identity only through the gateway |
| **II. Clean Architecture Layering** | **Pass on the dependency rule; one convention deviation, in Complexity Tracking.** Domain holds a plain entity; Application declares `IEmailTemplateStore` and `IHtmlSanitizer` and holds the pure rendering (`EmailHtml`, `EmailTemplates`, `EmailComposer`); Ganss.Xss, SQL and SMTP stay in Infrastructure; the controller only sends through MediatR. The use cases are not foldered one per `Commands/<UseCase>/` - they share one file, as the email code of specs/060 already did |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** A version and its audit entry (`AuditEntryRecorded`, staged through Identity's outbox) commit in one transaction with one `SaveChangesAsync`, and the entry is staged only when the `INSERT ... ON CONFLICT DO NOTHING` inserted - so the loser of a race records nothing. Nothing is ever updated, so there is no transition to repeat. No consumer was added. The test email is sent directly, outside any transaction, and changes no state |
| **IV. Identity Comes From the Token** | **Pass.** `[Authorize(Roles = "Admin")]` on the controller; `CreatedBy` and the test email's recipient come from `ICurrentUser`; no request carries a user id or an address. The storefront's `RequireRole` only draws |
| **V. Evidence Over Assumption** | **Pass.** The race, the unique index and the audit's transaction are tested against a real PostgreSQL; seven mutations were run and two that survived at first got tests of their own; Bruno ran 256/256 through the rebuilt storefront container; and a live email was read in Mailpit. Unverified, and said so: the 403 for a moderator is checked by Bruno on the list endpoint only, and how the HTML renders in real mail programs was not recorded |

**Post-design re-check**: no principle is violated. The one deviation from a convention the constitution states
(foldering by use case) is recorded below rather than waived.

## Project Structure

### Documentation (this feature)

```text
specs/077-email-templates/
├── spec.md                  # Feature specification
├── plan.md                  # This file
├── research.md              # D1-D14: decisions with rejected alternatives
├── data-model.md            # email_template_versions and its migration
├── quickstart.md            # Validation scenarios
├── contracts/
│   └── http-api.md          # The seven admin endpoints; the messaging it relies on; the SMTP message
├── checklists/
│   └── requirements.md      # Spec quality checklist
└── tasks.md                 # Task list, all done
```

### Source Code (touched by #161)

```text
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/EmailTemplateVersion.cs                     # new
├── Ecommerce.Identity.Application/
│   ├── Email/EmailTemplateEditing.cs          # new: store/sanitiser interfaces, composer, commands, queries, validators, handlers
│   ├── Email/EmailHtml.cs                     # new: placeholders, plain text to HTML, escaping, HTML to text
│   ├── Email/EmailTemplates.cs                # defaults as HTML, placeholder lists, SampleData, Render(..., edited)
│   ├── Email/EmailInterfaces.cs               # IEmailTransport.SendAsync(to, subject, text, html)
│   ├── Email/DispatchEmailsCommand.cs         # renders through EmailComposer
│   └── DependencyInjection.cs                 # EmailComposer
├── Ecommerce.Identity.Infrastructure/
│   ├── Email/AllowListHtmlSanitizer.cs        # new (Ganss.Xss)
│   ├── Email/EmailTemplateStore.cs            # new: INSERT ... ON CONFLICT DO NOTHING
│   ├── Email/SmtpEmailTransport.cs            # multipart/alternative
│   ├── Configurations/EmailTemplateVersionConfiguration.cs                        # new
│   ├── Migrations/20260926104309_AddEmailTemplateVersions.cs (+ Designer, snapshot)
│   ├── Persistence/ApplicationDbContext.cs    # DbSet
│   ├── DependencyInjection.cs                 # store, sanitiser
│   └── Ecommerce.Identity.Infrastructure.csproj   # HtmlSanitizer 9.2.1039
└── Ecommerce.Identity.WebApi/Controllers/EmailTemplatesController.cs              # new

server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json    # email-templates-route, email-templates-root-route
server/tests/Ecommerce.Identity.Tests/
├── EmailTemplateTests.cs                      # new, 13 tests
└── IdentityTestFixture.cs                     # store, sanitiser; FakeEmailTransport records the HTML too

client/
├── package.json, package-lock.json            # TipTap
└── src/
    ├── components/shared/rich-text-editor/index.tsx (+ index.test.tsx)
    ├── services/email-template/index.ts, types.ts (+ index.test.ts)
    ├── hooks/email-template/index.ts
    ├── pages/admin-emails/index.tsx, email-editor.tsx, email-versions.tsx (+ index.test.tsx)
    ├── routes/index.tsx                       # lazy route behind RequireRole Admin
    ├── layouts/admin-layout/index.tsx         # Emails menu item, adminOnly
    ├── constants/query-keys/index.ts
    ├── index.css                              # editor styles
    └── locales/{vi,en}/admin.json, common.json

bruno/admin-users/                             # 17-23: 403, list, 400 by name, preview, save, 409, reset
docs/features/email.md, docs/reference/{api,data-model,gateway}.md, docs/project/{timeline,backlog}.md,
docs/overview/project-overview.md, docs/testing/testing-strategy.md, CLAUDE.md
```

**Structure Decision**: everything server-side sits in Identity's existing `Email/` folders from specs/060, beside
the dispatcher it changes. The storefront follows the client README's layout: a service class per entity, a hook
layer, a page folder with supporting files beside its `index.tsx`, and a shared component for the editor.

## Complexity Tracking

| Deviation | Why it was needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| The seven use cases share `Application/Email/EmailTemplateEditing.cs` and one handler class, `EmailTemplateHandlers`, instead of `Email/Commands/<UseCase>/` folders (Principle II's foldering rule), and the two interfaces are declared there rather than under `Common/Interfaces/` | Not recorded. The code follows the flat `Email/` folder specs/060 had already established (`DispatchEmailsCommand.cs`, `EmailInterfaces.cs`), and the use cases share their private helpers (`Clean`, `AddVersionAsync`, `Effective`) | Not recorded. The dependency direction - the part of Principle II stated "without exception" - is kept |

## What this feature does not finish

- **Notification wording** is the other half of #150, done in [specs/078](../078-notification-wording/), which
  closes the issue.
- **Three emails only.** Other notices were not emails yet; eight more came in
  [specs/083](../083-more-emails/) (#171), editable the same way.
- **No images**, deliberately: a remote image in an email is also a read receipt. No logo is therefore possible.
- **No screen of failed emails**; the rows keep the reason. (Later: [specs/087](../087-email-delivery/).)
- **No per-shop templates, no new templates from the console, no attachments.**
- **A toast could be lost.** The page saved with `mutate(..., { onSuccess })` while the editor's `key` includes the
  version, so a successful save remounted the editor and its toast callback never ran. This was found after the
  merge by the browser tests of [specs/080](../080-e2e-browser/) (#164), which moved save, reset and restore to
  `mutateAsync().then(...)`.
