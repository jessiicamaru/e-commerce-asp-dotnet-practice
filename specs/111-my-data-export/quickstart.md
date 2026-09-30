# Quickstart: A person downloads the data the shop holds about them

## Scenario 1 - Tests

```bash
cd server
DB_PASSWORD=... dotnet test --filter "MyData"
cd ../client && npx vitest run src/pages/account src/utils/account
```

Expected in each of the six services:
- the inventory test: every model table is declared;
- the sections test: every exported table is a section;
- the two-people test: each export holds its owner's rows and none of the other's.

## Scenario 2 - Through the gateway (Bruno `auth/` and `security-checks/`)

- The customer's six exports each answer 200 with `service`, `sections` and `withheld`.
- The Identity export's `withheld` names `refresh_tokens`, and nothing in it contains `passwordHash`.
- The same six routes without a token answer 401.

## Scenario 3 - In the storefront

On `/account`, press "Download my data": the page downloads one `my-data-<date>.json`. Stop Payment and press it
again: the file marks `payment` unavailable, and the page says so.

## Scenario 4 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| A table dropped from an inventory | the inventory test |
| An exported section removed from a handler | the sections test |
| A filter by the caller removed | the two-people test |
| The password hash, or a full payout account number, added to Identity's export | the secrets test |
| An audit entry's actor exported on an entry about the person | Activity's test |
| The storefront drops a failed service silently | the page test |
