# Contracts: Old notices and audit entries are removed on a schedule

No HTTP endpoint, message or gRPC shape changes.

## Audit entry written by the sweeper

`AuditEntryRecorded` (existing contract), recorded by Activity itself:

| Field | Value |
| :-- | :-- |
| Category | `System` |
| Action | `AuditTrimmed` |
| SubjectType / SubjectId | `AuditLog` / none |
| Summary | `Audit entries before {cutoff:yyyy-MM-dd} removed: {count}` |
| After | `{ "cutoff": "...", "removed": n, "years": y }` |
| Actor | none (the system) |

## Configuration (environment)

`Retention__ReadNotificationDays`, `Retention__AuditYears`, `Retention__BatchSize`, `Retention__IntervalMinutes`.
