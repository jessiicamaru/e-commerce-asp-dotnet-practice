# Quickstart: Notice wording is edited like the emails

## Scenario 1 - In a browser

Open `/admin/notifications`, search "banned", choose it, switch to Tiếng Việt. Expected: one screen; the address carries `?kind=AccountBanned&lang=vi`.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/pages/admin-wording
```
