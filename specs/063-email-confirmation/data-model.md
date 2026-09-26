# Phase 1 Data Model: Email confirmation

> Written on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/db-design.md](../../docs/features/auth/db-design.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One migration in `ecommerce_identity_db`, `20260925060135_AddEmailConfirmation`, in two parts: a nullable column on
`users` with a backfill, and a new table. The confirmation email uses the existing `outgoing_emails`.

---

## `users.EmailConfirmedAt` (new column)

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `EmailConfirmedAt` | timestamptz | Nullable | When the link sent to the address was used. Null: not yet. `User.EmailConfirmed` is `EmailConfirmedAt is not null` (not mapped) |

**Backfill**, in the same migration:

```sql
UPDATE users SET "EmailConfirmedAt" = "CreatedAt" WHERE "EmailConfirmedAt" IS NULL;
```

On the development database it confirmed 32 of 32 existing accounts, each with `EmailConfirmedAt = CreatedAt`
(PR #146). `DataInitializer` sets it for the administrator it seeds.

**Why it is safe for an older image**: adding a nullable column is additive (constitution, "Schema evolution"); an
earlier Identity image neither reads nor writes it, and its new accounts are simply left null.

---

## `email_confirmation_tokens` (new table)

Mapped by `EmailConfirmationTokenConfiguration` from `EmailConfirmationToken`; the same shape as
`password_reset_tokens` (specs/061).

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK (v7 from the code) | |
| `UserId` | uuid | Not null, FK `FK_email_confirmation_tokens_users_UserId` → `users.Id`, on delete cascade, indexed | Whose link |
| `TokenHash` | character(64) | Not null, fixed length, unique (`IX_email_confirmation_tokens_TokenHash`) | Hex SHA-256 of the token |
| `ExpiresAt` | timestamptz | Not null | `CreatedAt` + 24 hours |
| `UsedAt` | timestamptz | Nullable | Set by the claim |
| `CreatedAt` | timestamptz | Not null | Also what the resend interval asks |

---

## States

```text
  account:  registered (EmailConfirmedAt null) ──link used──▶ confirmed (EmailConfirmedAt set)   [one way]

  link:     unused ──confirm, in 24 h──▶ used
               ├──── resend ───────────▶ deleted
               └──── 24 h pass ────────▶ expired (row stays)
```

| Step | Rows, in one save or transaction |
| :--- | :--- |
| Register (either kind) | `users` row (`EmailConfirmedAt` null), the token row, one `outgoing_emails` row (`EmailConfirmation`, `{"token": ...}`), `EmailConfirmationSent` in the outbox - one save |
| `POST /api/auth/confirm-email` | claim the token; `UPDATE users SET "EmailConfirmedAt" = now, "UpdatedAt" = now WHERE "Id" = @id AND "EmailConfirmedAt" IS NULL`; `EmailConfirmed` in the outbox - one transaction |
| `POST /api/auth/resend-confirmation` | lock the `users` row; if no link in the last minute: delete unused tokens, add one, stage the email, `EmailConfirmationSent` - one transaction |
| Dispatch, once sent | `outgoing_emails.DataJson` = `{}` |

## Read by other rules

- `ShopApplicationRow` gains the applicant's confirmation (`u.EmailConfirmedAt != null`), used to refuse an
  approval (409) and returned to staff as `ApplicantEmailConfirmed`.
- `AuthResponse.EmailConfirmed` is filled from the user by login, refresh and both registrations.
