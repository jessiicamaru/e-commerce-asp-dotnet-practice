# Quickstart: The client becomes a workspace of apps and packages

## Scenario 1 - The same checks, from `client/`

```bash
cd client
npm ci
npm run lint && npm test && npm run build
```

Expected: lint clean, 693 tests plus the layering test pass, and the build is written to `apps/storefront/dist`.

## Scenario 2 - In a browser

```bash
cd client && npm run dev        # http://localhost:5173
```

Expected: the storefront looks exactly as before, and the kit's components (buttons, cards, selects) are styled.

## Scenario 3 - The image and the flows

```bash
docker build -t ecommerce-storefront:local client && .github/scripts/verify-storefront-image.sh ecommerce-storefront:local
cd client && npm run e2e
```

Expected: both pass.
