# Data Model: Shoppers report a review, a question or a product

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

## Catalog: `content_reports` (migration `20260927020308_AddContentReports`)

A new table, so the change is expand only.

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` | Key, v7, `ValueGeneratedNever`. |
| `TargetType` | `varchar(16)` | `Review`, `Question` or `Product` (enum as text). |
| `TargetId` | `uuid` | The review, question or product. Not a foreign key, because it points at one of three tables. |
| `ProductId` | `uuid` | FK → `products`, `ON DELETE CASCADE`. The product itself, or the one the review or question hangs on. |
| `ReporterId` | `uuid` | From the token. |
| `Reason` | `varchar(16)` | `Spam`, `Offensive`, `Misleading`, `Counterfeit` or `Other`. |
| `Details` | `varchar(500)` null | The reporter's own words, trimmed; null when none. |
| `Status` | `varchar(16)` | `Open`, `Actioned` or `Dismissed`. No database default and no `HasDefaultValue` (the specs/093 enum trap). |
| `CreatedAt` | `timestamptz` | |
| `ResolvedAt`, `ResolvedBy` | `timestamptz` null, `uuid` null | Who closed it and when. |

Indexes:

- `UX_content_reports_open_per_reporter`: unique (`TargetType`, `TargetId`, `ReporterId`) `WHERE "Status" = 'Open'`.
- `IX_content_reports_TargetType_TargetId`: (`TargetType`, `TargetId`) `WHERE "Status" = 'Open'`, for the queue and
  closing.
- `IX_content_reports_ProductId`, from the foreign key.

## State transitions

```text
Open ──(hide review / hide question / hide answer / take product down)──▶ Actioned
Open ──(dismiss)───────────────────────────────────────────────────────▶ Dismissed
```

Each transition is one statement for every open report of a thing:

```sql
UPDATE content_reports SET "Status" = @outcome, "ResolvedAt" = @now, "ResolvedBy" = @by
 WHERE "TargetType" = @type AND "TargetId" = @id AND "Status" = 'Open'
RETURNING "ReporterId"
```

A closed report is never reopened. The same person reporting again inserts a new `Open` row.

## Notices (Shared `notification-kinds.json`)

| Kind | Required data |
| :-- | :-- |
| `ReportActioned` | `product` |
| `ReportDismissed` | `product` |
