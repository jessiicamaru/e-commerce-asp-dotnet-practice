# Quickstart: Security headers on both apps

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
curl -sI http://localhost:8088/ | grep -iE 'content-security|x-frame|nosniff|referrer|permissions|cross-origin|server'

# the image check, both apps
cd client && docker build -t ecommerce-storefront:ci . && docker build --build-arg APP=back-office -t ecommerce-back-office:ci .
cd .. && .github/scripts/verify-storefront-image.sh ecommerce-storefront:ci
.github/scripts/verify-storefront-image.sh ecommerce-back-office:ci back-office

# every browser flow, failing on any violation (ADMIN_* from server/.env)
cd client && npx playwright test
# the VNPay flows too
cd ../server && PAYMENT_PROVIDER=VnPay docker compose -f docker-compose.yml -f docker-compose.app.yml up -d payment
cd ../client && npx playwright test e2e/vnpay.spec.ts
```

Expected:
- the headers of [data-model.md](data-model.md) on every answer, and the image check all `ok`;
- 8 flows pass on the stub, and 3 more with VNPay, with no violation;
- ZAP's baseline: `FAIL-NEW: 0`, `WARN-NEW: 1` (10055);
- through Caddy over HTTPS, `Strict-Transport-Security: max-age=31536000` and no `Server` header.
