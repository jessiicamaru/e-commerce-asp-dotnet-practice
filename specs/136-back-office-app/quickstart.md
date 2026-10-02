# Quickstart: A back office for staff, with its own sign-in

## Scenario 1 - In development

```bash
cd client
npm run dev:back-office     # http://portal.localhost:5174
```

Sign in as the administrator. Get the code with `python server/seed/two_factor.py`. Expected: "Signed in as ...".
Then sign in to `http://localhost:5173` as a customer in the same browser. Expected: the back office stays signed in
as the administrator.

## Scenario 2 - Tests

```bash
cd client && npm test       # includes the back-office project
npm run e2e                 # includes the back-office flow (compose running)
```

## Scenario 3 - The image

```bash
docker build --build-arg APP=back-office -t ecommerce-back-office:local client
.github/scripts/verify-storefront-image.sh ecommerce-back-office:local back-office
```
