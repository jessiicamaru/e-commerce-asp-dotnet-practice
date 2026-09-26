# Data Model: An administrator edits the emails

> Written on 2026-09-27, after the feature merged (#161), from the code at that merge, the pull request and docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in Identity's database, `ecommerce_identity_db`, added by the migration
`20260926104309_AddEmailTemplateVersions`. No other table and no other service's database changed. The generated
reference is [docs/reference/data-model.md](../../docs/reference/data-model.md#email_template_versions).

---

## `email_template_versions`

Entity `EmailTemplateVersion` (`Ecommerce.Identity.Domain/Entities/EmailTemplateVersion.cs`), configured by
`EmailTemplateVersionConfiguration`. One row per saved version of one email's words in one language: an
administrator's edit, a reset to the built-in words, or an earlier version restored.

| Column | Type | Null | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | no | PK, `Guid.CreateVersion7()`, `ValueGeneratedNever()` |
| `Template` | character varying(50) | no | An `EmailTemplate` constant: `OrderPaid`, `PasswordReset`, `EmailConfirmation` |
| `Language` | character varying(10) | no | `vi` or `en` |
| `Version` | integer | no | 1, 2, 3 ... per template and language |
| `IsDefault` | boolean | no | True: back to the words in code; `Subject` and `BodyHtml` are then null |
| `Subject` | character varying(200) | yes | The subject, one line, with `{placeholders}` |
| `BodyHtml` | character varying(20000) | yes | The body, sanitised on save - and again when sent |
| `CreatedAt` | timestamp with time zone | no | When this version was saved |
| `CreatedBy` | uuid | no | The administrator, from the token (`ICurrentUser`) |

**Keys and indexes**:

- `PK_email_template_versions` on `Id`.
- `IX_email_template_versions_Template_Language_Version`, **unique** on `(Template, Language, Version)`. It is the
  concurrency guard (research D2): two administrators saving at once both ask for N + 1, and the index lets exactly
  one of them have it, through `INSERT ... ON CONFLICT ("Template", "Language", "Version") DO NOTHING`. It is also
  the read path for the current version (`ORDER BY "Version" DESC LIMIT 1` per template and language).

**Constraints**:

- `CK_email_template_versions_words`: `"IsDefault" OR ("Subject" IS NOT NULL AND "BodyHtml" IS NOT NULL)` - a
  version is either the default or has both a subject and a body.
- The lengths 200 and 20,000 are enforced by the columns and, before them, by the command validators.

**What is not constrained in the database, and why**: `Template` and `Language` are not checked against a list -
the list is code (`EmailTemplates.Templates`, `Languages`) and the handlers 404 anything else before writing.
Version numbers are not forced to be contiguous by the database; the handler inserts only `current + 1`.

---

## Reading it

| Question | How |
| :--- | :--- |
| What does an email say now? | The highest `Version` for its template and language. None, or an `IsDefault` one, means the built-in words in `EmailTemplates` |
| Every email, current | One row per template and language whose `Version` is the maximum for that pair (a correlated subquery), merged in code with the built-in words for the pairs that have none |
| The history | Every row for a template and language, newest first; an `IsDefault` row is shown with the built-in words |

## Writing it - append-only

There are no states and no updates: rows are only inserted, never updated or deleted. The effect of each action:

| Action | Row inserted | Guard |
| :--- | :--- | :--- |
| Save | `Version = expected + 1`, `IsDefault = false`, the sanitised subject and body | `expected` = current version (409), unique index (409), placeholders (400) |
| Reset | `Version = expected + 1`, `IsDefault = true`, nulls | current is not already default (409), then as save |
| Restore version *v* | `Version = expected + 1`, *v*'s words sanitised and checked again, or `IsDefault = true` if *v* was a reset | *v* exists (404), then as save |

Each insert runs in one transaction, inside `CreateExecutionStrategy().ExecuteAsync`: the raw `INSERT ... ON
CONFLICT DO NOTHING`, then - only if it inserted one row - the audit entry is staged (`IAuditTrail.RecordAsync`,
which publishes `AuditEntryRecorded` through Identity's outbox) and `SaveChangesAsync` writes the outbox row; then
commit. A loser inserts nothing and stages nothing.

---

## Migration and older images

`AddEmailTemplateVersions` only creates the table, its unique index and its CHECK. It is expand-only: an earlier
image runs against the migrated schema unchanged, never reads the table, and sends the built-in words (as plain
text, since the earlier transport sends one body). Rolling back therefore loses the edits' effect, not the edits.
`Down` drops the table.

## What did not change

- `outgoing_emails` (specs/060) is untouched: it still stores the template name, the data and the language, never
  rendered words. Words are chosen when an email is **sent**, so an edit applies to every email sent after it,
  including ones queued before it.
- No message contract changed; see [contracts/http-api.md](./contracts/http-api.md#messaging).
