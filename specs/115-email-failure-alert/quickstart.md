# Quickstart: Administrators are told when an email fails for good

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "EmailFailureAlert"
cd ../client && npx vitest run src/utils/notifications src/pages/admin-overview
```

Expected: an email reaching its last attempt produces one `EmailsFailed` per administrator with `failed: 1`, linked to
the email log; a second failure within the hour produces nothing and is counted by the next notice after the hour; a
retried email that fails again is counted again; two sweeps at once count each failure once; a moderator is not told.

## Scenario 2 - In the stack

Stop Mailpit, queue an email, let the dispatcher exhaust it (or set `EMAIL_MAX_ATTEMPTS` low); the administrator's bell
shows "1 email could not be delivered", linking to `/admin/email-delivery`; the Overview shows 1 failed.

## Scenario 3 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| The claim not guarded by the hour | the within-the-hour test |
| The claim without the uncounted filter | the counted-once test |
| The notice staged for moderators too | the administrators-only test |
| The retry not clearing the mark | the retried test |
| The notices not staged | the notice test |
