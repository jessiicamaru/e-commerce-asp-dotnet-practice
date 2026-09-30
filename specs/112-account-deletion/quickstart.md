# Quickstart: A person deletes their account

## Scenario 1 - Tests

```bash
cd server
DB_PASSWORD=... dotnet test --filter "AccountDeletion|MyData|AccountStanding"
cd ../client && npx vitest run src/pages/account
```

Expected:
- Identity: a deleted account's row is empty, its other rows gone, `AccountDeleted` and `AccessTokensRevoked`
  published; its email registers again; the old password signs nobody in; a wrong password changes nothing and counts;
  staff are refused; each blocker from Order is a 409 that changes nothing; Order unreachable is a 503.
- Order: each blocker is reported for the right person, and none for somebody else's business.
- Each of Identity, Catalog, Order, Cart, Payment and Activity: after the erasure, erased sections are empty and the
  export holds none of the planted personal values.

## Scenario 2 - Through the gateway (Bruno `my-data/`, after the export requests)

A new customer registers, adds an address, then:
- `DELETE /api/auth/me` with a wrong password: 400.
- With the right one: 204. Their token then answers 401; signing in with the old email and password answers 401, as a wrong password does.
- Registering the same email again: 201.
Without a token: 401 (`security-checks`).

## Scenario 3 - In the storefront

On `/account`, "Delete my account": type the password, confirm; the page signs out and says the account is gone. With
an order on its way, the page names that instead and nothing changes.

## Scenario 4 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| An Identity table left undeleted (addresses) | Identity's deletion test |
| The email not replaced | the re-registration test |
| `AccessTokensRevoked` not published | Identity's deletion test |
| A blocker ignored (open orders) | Identity's blocker test and Order's standing test |
| A blocker filter widened to everybody | Order's standing test |
| A consumer's erasure skipped (Catalog saved products, Activity notices, Cart) | that service's erasure test |
| The review's author name kept | Catalog's erasure test |
| The delivery copy's street kept | Order's erasure test |
| The email left in an audit summary | Activity's erasure test |
| A section missing from `Kept` but not erased | that service's erasure test |
| The storefront ignoring a refusal's reasons | the page test |
