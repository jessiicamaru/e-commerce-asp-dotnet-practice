# Phase 1 Data Model: Password reset

> Written on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/auth/db-design.md](../../docs/features/auth/db-design.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_identity_db`, added by migration `20260925035705_AddPasswordResetTokens`. The reset
email uses the existing `outgoing_emails` (specs/060); `users.PasswordHash` and `refresh_tokens.RevokedAt` are
existing columns this feature writes.

---

## `password_reset_tokens`

Mapped by `PasswordResetTokenConfiguration` from `PasswordResetToken` (Identity Domain).

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK, `ValueGeneratedNever` (v7 from the handler) | |
| `UserId` | uuid | Not null, FK `FK_password_reset_tokens_users_UserId` → `users.Id`, **on delete cascade**, indexed | Whose link |
| `TokenHash` | character(64) | Not null, fixed length, **unique** (`IX_password_reset_tokens_TokenHash`) | Hex SHA-256 of the token; never the token |
| `ExpiresAt` | timestamptz | Not null | `CreatedAt` + 30 minutes |
| `UsedAt` | timestamptz | Nullable | Set by the claim |
| `CreatedAt` | timestamptz | Not null | |

**Indexes**: unique on `TokenHash` (the claim's lookup), `IX_password_reset_tokens_UserId` (deleting a person's
unused links).

---

## A link's life

```text
   forgot-password ──▶ unused (UsedAt null, ExpiresAt in the future)
                          │
      reset, in time ─────┼──────────▶ used (UsedAt set)          ── further use: 400
                          │
      forgot-password ────┼──────────▶ deleted (DeleteUnusedAsync) ── use: 400
                          │
      30 minutes pass ────┴──────────▶ expired (row stays, ExpiresAt past) ── use: 400
```

The claim `UPDATE ... WHERE "UsedAt" IS NULL AND "ExpiresAt" > now RETURNING "UserId"` is the only transition out
of "unused" that grants anything; every other outcome is the same 400.

## Rows written per step

| Step | Rows, in one transaction |
| :--- | :--- |
| `POST /api/auth/forgot-password` (real account) | delete the person's unused `password_reset_tokens`; insert one; `PasswordResetRequested` in the outbox (for Activity); one `outgoing_emails` row, template `PasswordReset`, `DataJson` `{"token": "..."}` |
| `POST /api/auth/reset-password` | claim the token; `users.PasswordHash`, `users.UpdatedAt`; `PasswordReset` in the outbox; `refresh_tokens.RevokedAt` for every active token of the person |
| Dispatch, once sent | `outgoing_emails.DataJson` = `{}` (with `Status` = `Sent`) |

## Rollback

A new table only. An earlier image ignores it; a reset email already queued would then fail at dispatch as an
unknown template (specs/060 behaviour), and no link could be used.
