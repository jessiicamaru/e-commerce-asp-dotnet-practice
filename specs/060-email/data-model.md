# Phase 1 Data Model: Email

> Written on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_identity_db`, added by migration `20260925032037_AddOutgoingEmails`. Nothing else in
any service's schema changed: Order's request travels through its existing outbox tables, and
`OrderNoticeFacts.Language` is read from the existing `orders.Language` column.

---

## `outgoing_emails`

Mapped by `OutgoingEmailConfiguration` from `OutgoingEmail` (Identity Domain).

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK, `ValueGeneratedNever` | The requester's `EmailId`; what makes a redelivery a no-op |
| `RecipientId` | uuid | Not null, indexed | The person; address and name are read from `users` at send time |
| `Template` | varchar(64) | Not null | e.g. `OrderPaid` |
| `DataJson` | jsonb | Not null | The request's data, e.g. `orderId`, `total`, `currency` |
| `Language` | varchar(10) | Not null | Lower-cased; empty in the request becomes `vi` |
| `Status` | varchar(20) | Not null, enum as string | `Pending`, `Sent`, `Failed` |
| `Attempts` | integer | Not null | Sends tried (a terminal failure counts as one) |
| `NextAttemptAt` | timestamptz | Not null | When the sweeper may try; `CreatedAt` at first |
| `SentAt` | timestamptz | Nullable | Set when `Sent` |
| `LastError` | varchar(1000) | Nullable | The last failure, truncated to 1000 characters; cleared on success |
| `CreatedAt` | timestamptz | Not null | |

**Indexes**:

- `IX_outgoing_emails_due` on `NextAttemptAt` **filtered** `"Status" = 'Pending'` - what the sweeper asks, over
  pending rows only.
- `IX_outgoing_emails_RecipientId` on `RecipientId`.

**Writes**:

- Queue: `INSERT ... ON CONFLICT ("Id") DO NOTHING` (raw SQL in `OutgoingEmailRepository.QueueAsync`).
- Claim: `SELECT * ... WHERE "Status" = 'Pending' AND "NextAttemptAt" <= now ORDER BY "NextAttemptAt" LIMIT
  batch FOR UPDATE SKIP LOCKED`, inside the dispatch transaction; the changes are saved under that lock.

---

## State transitions

```text
   EmailRequested ──▶ Pending ──send ok──────────────────────────▶ Sent
                        │  ▲
                        │  └── send throws, Attempts < 12: NextAttemptAt = now + 1, 2, 4 ... 60 min
                        │
                        ├── send throws, Attempts reaches 12 ────────▶ Failed (LastError = exception message)
                        └── no such recipient / no words / data incomplete ──▶ Failed (at once, never retried)
```

| Transition | Columns written |
| :--- | :--- |
| → `Pending` | all request columns, `Attempts` 0, `NextAttemptAt` = `CreatedAt` |
| `Pending` → `Pending` (retry) | `Attempts`++, `LastError`, `NextAttemptAt` |
| `Pending` → `Sent` | `Status`, `SentAt`, `Attempts`++, `LastError` = null |
| `Pending` → `Failed` | `Status`, `Attempts`++, `LastError` |

`Sent` and `Failed` are terminal in this feature (a failed one sent again came with specs/087).

## Rollback

A new table only: an older Identity image ignores it. An older Order image simply requests no email.
