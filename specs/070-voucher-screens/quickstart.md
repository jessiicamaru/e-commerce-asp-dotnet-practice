# Quickstart: Validating the voucher screens

> Written on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/README.md](contracts/README.md)

## Prerequisites

```bash
cd client && npm ci
cd ../server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # storefront on :8088
```

---

## Scenario 1 - Checkout and order page (US1, SC-001, SC-002)

```bash
cd client
npm test -- src/pages/checkout src/pages/order src/services/voucher
```

**Expected**: the checkout tries a code with the server before keeping it, then quotes and places the order with it;
a refused code is shown in the server's words and the summary stays as it was; removing a code quotes without it; the
quote repeats `voucherCodes=` per code; the order page lists each voucher by code, a shop's with its name, and each
line's discount.

---

## Scenario 2 - The voucher pages and form (US2, US3, SC-003)

```bash
npm test -- src/pages/shop-vouchers src/components/voucher src/utils/voucher src/layouts/admin-layout
```

**Expected**: the seller's page lists vouchers in words, creates one for the shop with no free delivery offered and
products from the seller's own (`Product.list` not called), shows a refusal inside the form, disables only after
confirming; the admin's page offers free delivery and new customers and searches the whole catalogue; `toNewVoucher`
sends each role's request (a fixed amount with no cap, free delivery with no products, dates as ISO instants);
`describe` words the benefit per currency, the conditions and the uses against limits; the admin menu shows Vouchers to
an administrator and not to a moderator.

---

## Scenario 3 - The whole suite and the live stack (SC-004)

```bash
npm test && npm run lint && npx tsc -b && npm run build        # PR: 399/399 in 67 files
for p in shop/vouchers admin/vouchers checkout; do
  curl -s -o /dev/null -w "$p %{http_code}\n" http://localhost:8088/$p
done                                                             # 200 each
cd ../bruno && npx @usebruno/cli run --env local --env-var "baseUrl=http://localhost:8088" \
  --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
# PR: 229/229 requests, 376/376 tests - including the checkout claiming a voucher
```

---

## Scenario 4 - By hand in a browser (not done at merge)

As a seller, create "10% off, up to ₫100,000, on orders from ₫500,000" at `/shop/vouchers`; as a customer with that
seller's goods in the cart, apply the code at `/checkout`, see it as a chip with the seller's name and its amount, place
the order, and find the same on `/orders/:id`. Try a code below its minimum and read the refusal beside the box. The PR
states this click-through was **not done**.
