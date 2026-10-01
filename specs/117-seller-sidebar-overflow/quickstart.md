# Quickstart: The seller sidebar keeps to its column

## Scenario 1 - In a browser

Sign in as a seller whose shop name is long (rename it to 60 characters). Open `/shop/sales/<id>`. Expected: the name ends in an ellipsis, the page title and "Start preparing" are fully visible.

## Scenario 2 - The flows

```bash
cd client && E2E_BASE_URL=http://localhost:5173 npx playwright test
```

Expected: green; with `grid-cols-[minmax(0,1fr)]` removed from the aside, the sidebar assertion fails.
