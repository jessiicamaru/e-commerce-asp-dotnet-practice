# Quickstart: Staff roles only in a back-office session

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter BackOfficeSession
```

## Scenario 2 - By hand

```bash
# the code: python server/seed/two_factor.py
curl -s -X POST localhost:5000/api/auth/login -H 'Content-Type: application/json' -d '{"email":"...","password":"..."}'
curl -s -X POST localhost:5000/api/auth/login/two-factor -H 'Content-Type: application/json' -H 'Origin: http://localhost:8088' -d '{"challenge":"...","code":"..."}'
```

Expected: `roles` has no `Admin`, and `staffAccount: true`. With `-H 'Origin: http://portal.localhost:8089'` instead,
`roles` has `Admin`.

## Scenario 3 - Everything that acts as staff

`verify-auth.sh`, `verify-saga.sh`, Bruno and `npm run e2e` all pass.
