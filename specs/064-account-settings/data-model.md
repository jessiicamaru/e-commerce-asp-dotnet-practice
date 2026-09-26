# Phase 1 Data Model: Change your password and your name

> Written on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/db-design.md](../../docs/features/auth/db-design.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** The feature writes columns that already existed in
`ecommerce_identity_db`, so any earlier image runs against the same schema.

---

## Columns written

| Table | Column | By | How |
| :--- | :--- | :--- | :--- |
| `users` | `FirstName`, `LastName` (trimmed), `PhoneNumber` (trimmed; blank becomes null), `UpdatedAt` | `PUT /api/auth/me` | tracked entity, one save with the audit entry |
| `users` | `PasswordHash`, `UpdatedAt` | `PUT /api/auth/me/password` | tracked entity, saved inside the transaction |
| `refresh_tokens` | `RevokedAt` | `PUT /api/auth/me/password` | `UPDATE refresh_tokens SET "RevokedAt" = now WHERE "UserId" = @id AND "RevokedAt" IS NULL AND (@keep IS NULL OR "Token" <> @keep)` (EF `ExecuteUpdate`), same transaction |
| `sign_in_throttles` | all (specs/062) | a wrong current password; a successful change | `RecordFailureAsync` / `ClearAsync` |
| MassTransit outbox | `AuditEntryRecorded` | both commands | `ProfileUpdated` (User, before/after of first name, last name, phone); `PasswordChanged` (Security, no snapshot) |

## Sessions after a password change

| Refresh token | Before | After |
| :--- | :--- | :--- |
| The one in the request's cookie | active | **active** |
| Every other active one of the user | active | revoked (`RevokedAt` = now) |
| No cookie sent | - | every active one revoked |

A revoked token that was never rotated is refused quietly at its next refresh (specs/058).
