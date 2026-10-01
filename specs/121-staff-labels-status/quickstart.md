# Quickstart: Every audit action has words, and the status page lists every service

## Scenario 1 - Labels

Open `/admin/moderation` and `/admin/audit` in both languages. Expected: no PascalCase action names.

## Scenario 2 - Status

Open `/status`. Expected: eight services by name, each up or down; no note about the orchestrator.

## Scenario 3 - Tests

```bash
cd client && npx vitest run src/locales/audit-actions.test.ts src/services/health
```

Expected: green; with one label removed, or one service removed from the list, red.
