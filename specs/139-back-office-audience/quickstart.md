# Quickstart: A separate token audience for the back office

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "BackOfficeAudience|BackOfficeSession"
```

Expected: a storefront-audience token with `Admin` is 403 on a staff endpoint, a back-office one 200, and neither 401.
Then Bruno, `verify-auth.sh`, `verify-saga.sh` and `npm run e2e` pass unchanged.
