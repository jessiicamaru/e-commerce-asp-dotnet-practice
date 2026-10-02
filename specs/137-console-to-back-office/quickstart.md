# Quickstart: The admin and moderator console moves to the back office

## Scenario 1 - Tests

```bash
cd client && npm test && npm run build
```

Expected: the console's tests pass in the `back-office` project, and the storefront has no `admin-*` page.

## Scenario 2 - In a browser

1. Open `http://portal.localhost:5174` and sign in as the administrator. Expected: the fulfilment queue with the grouped menu.
2. Open `http://localhost:5173/admin/products`. Expected: the back office's `/products`.
3. On the storefront, open the account page as staff. Expected: "Go to management platform" opens the back office.

## Scenario 3 - Against compose

```bash
cd client && npm run e2e
```

Expected: the moderator approves a product in the back office.
