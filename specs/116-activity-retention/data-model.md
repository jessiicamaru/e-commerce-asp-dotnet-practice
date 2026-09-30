# Data model: Old notices and audit entries are removed on a schedule

## Activity

| Table | Change |
| :-- | :-- |
| `notifications` | Partial index `IX_notifications_ReadAt` on `("ReadAt") WHERE "ReadAt" IS NOT NULL` - migration `AddRetentionIndexes`. |
| `audit_entries` | No change: `IX_audit_entries_OccurredAt` exists. |

Deleted by the sweeper: `notifications` rows with `"ReadAt" < now - ReadNotificationDays`; `audit_entries` rows with
`"OccurredAt" < now - AuditYears` when that is set.

Written by the sweeper: one `audit_entries` row (through `AuditEntryRecorded`, the service's own outbox) per trim that
deleted audit entries: category `System`, action `AuditTrimmed`, subject `AuditLog`, summary with the cutoff and count.

## Configuration

| Setting | Default | Range |
| :-- | :-- | :-- |
| `Retention:ReadNotificationDays` | 90 | 1 or more |
| `Retention:AuditYears` | none (keep for ever) | 1 or more when set |
| `Retention:BatchSize` | 1000 | 1 or more |
| `Retention:IntervalMinutes` | 60 | 1 or more |
