# Quickstart: A one-time handoff from the storefront to the back office

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter Handoff
cd ../client && npx vitest run apps/back-office/src/pages/auth-callback packages/core/src/utils/back-office
```

## Scenario 2 - In a browser

1. Sign in to `http://localhost:8088` as a moderator, with the code.
2. Click "Management platform". Expected: `portal.localhost:8089` asks for the code only, and the console opens after it.
3. Reload `portal.localhost:8089/auth/callback`. Expected: the console, because the session exists; the fragment is gone.
