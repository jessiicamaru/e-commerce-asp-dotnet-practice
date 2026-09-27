# Data Model: Staff see a person's moderation history

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

## Activity: `audit_entries` (migration `20260927013555_AddAuditAboutUser`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `AboutUserId` | `uuid` null | **New.** The person the entry is about. Null for entries about nobody in particular and for content decisions recorded before this feature. |

A new index, `IX_audit_entries_AboutUserId_OccurredAt`, supports reading one person's history newest first.

The migration adds a nullable column (expand only), then backfills it:

```sql
UPDATE audit_entries SET "AboutUserId" = "SubjectId"::uuid
 WHERE "SubjectType" = 'User'
   AND "SubjectId" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';
```

The rows stay immutable after the backfill. `RecordAuditEntryCommand` writes the column in the same `INSERT ... ON
CONFLICT ("Id") DO NOTHING` as the rest of the entry.

## Read

```sql
SELECT ... FROM audit_entries
 WHERE "AboutUserId" = @userId AND "Category" = 'Moderation'
 ORDER BY "OccurredAt" DESC, "Id" DESC
 OFFSET ... LIMIT ...
```

The reason is extracted in the handler from `After` (research D2), and only it is returned.

## Who is named, per entry

| Action | Publisher | `AboutUserId` |
| :-- | :-- | :-- |
| `AccountLocked`, `AccountUnlocked`, `AccountBanned`, `BanLifted`, `RoleGranted`, `RoleRevoked` | Identity | the user (subject; filled by `AuditTrail`) |
| `ShopApproved`, `ShopRejected` | Identity | the applicant |
| `ReviewHidden`, `ReviewRestored` | Catalog | the review's author |
| `QuestionHidden`, `QuestionRestored` | Catalog | the asker |
| `AnswerHidden`, `AnswerRestored` | Catalog | the answer's author |
| `ProductApproved`, `ProductRejected`, `ProductTakenDown` | Catalog | the product's seller (null for the shop's own) |
| `ProductSentForReview`, `ProductResubmitted` | Catalog | null: the seller's own act (research D5) |

## State transitions

None.
