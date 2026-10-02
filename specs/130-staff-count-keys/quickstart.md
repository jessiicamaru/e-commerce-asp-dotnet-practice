# Quickstart: Staff counts never share a list's cache

## Scenario 1 - Tests

```bash
cd client && npx vitest run src/pages/admin-moderation src/hooks
```

## Scenario 2 - In a browser

Open `/admin/moderation`, then Products to review. Expected: the full page at once; approve one - the counts drop.
