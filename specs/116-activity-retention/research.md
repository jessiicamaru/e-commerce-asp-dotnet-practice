# Research: Old notices and audit entries are removed on a schedule

## D1 - Read notices: 90 days after they were read

**Decision**: delete notices with `ReadAt` older than `Retention:ReadNotificationDays` (90). Unread notices are never
deleted.

**Rationale**: the issue's rule. Counting from `ReadAt`, not `CreatedAt`: a notice read yesterday was useful yesterday,
however old it is. Unread ones are somebody's unseen news - the bell's count would silently drop if they went.

**Alternatives rejected**: a cap per person (the newest N) - a busy seller would lose last week's sale notices while an
idle customer keeps years of them.

## D2 - The audit log: kept for ever unless an operator says, and never silently shortened

**Decision**: `Retention:AuditYears` is empty by default - nothing is deleted. When set (at least 1), entries older than
that many years go, and every batch that deletes some records a `System` / `AuditTrimmed` entry with the cutoff and
its count, in the batch's own transaction - an entry for the whole sweep could not commit with deletes spread over many
transactions, and a crash between them would trim the log without saying so.

**Rationale**: the audit log is the security record staff decisions are judged by (specs/041, 100); deleting it must be a
deliberate operator decision, and the log must say it was trimmed - otherwise a missing year looks like a quiet one.
Vietnam's accounting and cybersecurity rules ask for years of records; the default of "for ever" cannot break them, a
configured number is the operator's to choose.

**Alternatives rejected**: an archive (export to a file or cold table, then delete) - there is no reader for it and no
store to put it in beyond the database it would leave; the database backup already is one. Never allowing deletion -
the table would still grow without bound, which is the issue.

## D3 - Batches of short statements, safe on several instances

**Decision**: `DELETE FROM <table> WHERE "Id" IN (SELECT "Id" FROM <table> WHERE <old> LIMIT @batch)`, repeated until a
batch deletes fewer than the batch size.

**Rationale**: one statement over hundreds of thousands of rows would hold its locks and bloat the WAL in one go; short
batches let the bell's reads interleave. Two instances deleting the same rows: the second waits on the first's row locks
and then finds them gone - it deletes nothing and fails nothing, which is all "safe" needs here.

## D4 - Indexes for the sweep

**Decision**: a partial index on `notifications ("ReadAt") WHERE "ReadAt" IS NOT NULL`; the audit log's `OccurredAt`
is already indexed.

**Rationale**: without it the sweep reads the whole table every hour.

## D5 - Settings refused at startup

**Decision**: Activity refuses to start when `ReadNotificationDays < 1`, `AuditYears < 1` (when set), `BatchSize < 1` or
`IntervalMinutes < 1`, naming the setting - specs/103's rule that a setting is checked when the service starts, not
when it is first used.
