# Quickstart: The catalogue on a phone

## Scenario 1 - On a phone

Open `/` at 390px. Expected: two products a row, the first within 1,100px, Min – Max on one row, nothing scrolling sideways.

## Scenario 2 - Tests

```bash
cd client && E2E_BASE_URL=http://localhost:5173 npx playwright test -g phone
```
