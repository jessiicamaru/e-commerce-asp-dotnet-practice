# Quickstart: Staff see a deleted account as deleted

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter DeletedAccount
cd ../client && npx vitest run src/pages/admin-users
```

## Scenario 2 - In the stack

Delete a test customer's account, then open `/admin/users`. Expected: not listed; tick "Show deleted accounts" - listed as Deleted with no menu. `POST /api/users/{id}/lock` on it: 409 `AccountDeleted`.
