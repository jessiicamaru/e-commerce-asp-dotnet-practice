# Quickstart: Old notices and audit entries are removed on a schedule

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Activity.Tests --filter "Retention"
```

Expected: a notice read 91 days ago is deleted, one read 89 days ago and an unread one of any age stay; with no audit
retention nothing is deleted; with 5 years, entries older than 5 years go and one `AuditTrimmed` entry is recorded, and
none is recorded when nothing went; batches of 2 remove 5 rows; two sweeps at once remove each row once; every setting
out of range is refused.

## Scenario 2 - In the stack

With the Activity container running, mark a notice read 100 days ago in `ecommerce_activity_db`; restart Activity (the
sweep runs at start) - the notice is gone, an unread one of the same age stays.

## Scenario 3 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| Unread notices deleted too (the `ReadAt` filter removed) | the unread test |
| The cutoff ignored | the recent test |
| Audit entries deleted with no setting | the keep-for-ever test |
| No trim entry recorded | the trim test |
| The batch loop stops after one batch | the batches test |
| A setting below 1 accepted | the settings test |
