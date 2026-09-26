# Data Model: Email delivery

> Written on 2026-09-27, after the feature merged (#179), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration changed** (the pull request: "No migration"). The feature reads and updates
`outgoing_emails` (specs/060) and reads `users`.

---

## `outgoing_emails` (Identity) - as read

| Column | Type | Shown? |
| :--- | :--- | :--- |
| `Id` | `uuid`, PK (the requester's id, `ValueGeneratedNever`) | yes |
| `RecipientId` | `uuid`, indexed | yes |
| `Template` | `varchar(64)` | yes |
| `DataJson` | `jsonb` | **never** |
| `Language` | `varchar(10)` | yes |
| `Status` | `varchar(20)`, `Pending` / `Sent` / `Failed` | yes |
| `Attempts` | `integer` | yes |
| `NextAttemptAt` | `timestamp with time zone`; partial index `IX_outgoing_emails_due` where `Status = 'Pending'` | yes |
| `SentAt` | `timestamp with time zone`, nullable | yes |
| `LastError` | `varchar(1000)`, nullable | yes |
| `CreatedAt` | `timestamp with time zone` | yes, and the sort key (newest first, then `Id`) |

Joined: `users.Email` on `RecipientId` (left join - a recipient that is gone gives a null address), filtered with
`lower("Email") LIKE '%<search>%'` when a search is given. No index serves a contains search; not recorded as
measured.

## State transitions

```text
Pending ──sent──▶ Sent
   │
   └──12 failed attempts, or unknown recipient / template / data──▶ Failed
                                                                     │
                     POST /api/emails/{id}/retry (specs/087) ◀───────┘  → Pending, Attempts 0, due now
                     (not for PasswordReset or EmailConfirmation)
```

The dispatcher's transitions are specs/060's, unchanged. The one new transition is `Failed → Pending`:

```sql
UPDATE outgoing_emails
SET "Status" = 'Pending', "Attempts" = 0, "NextAttemptAt" = @now, "LastError" = NULL
WHERE "Id" = @id AND "Status" = 'Failed';
```

Zero rows affected means "not failed any more" and becomes a 409. `SentAt` and `DataJson` are not touched.

## Audit entry (Activity, through Identity's outbox)

| Field | Value |
| :--- | :--- |
| Category / action | `System` / `EmailRetried` |
| Subject | `Email`, the email's id |
| Summary | `A failed <Template> email was put back in the queue` |
| Before / after | `{ Status: "Failed", Attempts, LastError }` / `{ Status: "Pending", Attempts: 0 }` |

## Schema evolution

Nothing to evolve. An earlier Identity image simply has no `/api/emails`.
