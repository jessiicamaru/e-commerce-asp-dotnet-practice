# Quickstart: Validating the administrator's console

> Written on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
cd ../client && npm run dev        # or the storefront image on :8088
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
CUSTOMER=...; SELLER=...
```

A paid order holding the shop's goods and a seller's (the pull request used the shop's Canon and Tuấn's
battery), and a seller with money due (a delivered parcel past the return window, under today's rules -
specs/040 and 066 moved "due" later than it was at #82).

---

## Scenario 1 — Staff read any order; nobody else can (FR-001, SC-003)

```bash
curl -s http://localhost:5000/api/orders/fulfilment/$ORDER -H "Authorization: Bearer $ADMIN" | jq '.orderId, .shippingAddress, .shipments'
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/orders/fulfilment/$ORDER -H "Authorization: Bearer $CUSTOMER"
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/orders/fulfilment/$ORDER -H "Authorization: Bearer $SELLER"
curl -s http://localhost:5000/api/orders/fulfilment/$(uuidgen) -H "Authorization: Bearer $ADMIN" | jq .detail
```

**Expected**: the order with its address and every parcel (one with `isShop: true`); `403`; `403`;
`Order not found.` The customer's 403 holds even when it is their own order - this route is for staff.

## Scenario 2 — Work the shop's parcel in the browser (US1, SC-001)

Sign in to the storefront as the administrator. The header and the user menu show the console link.

1. `/admin` opens on the **waiting** tab (the state is in the URL); the order is listed.
2. Open it: the shop's items, the delivery address, every parcel, and one action - **prepare**.
3. Prepare, then ship with a tracking reference.

**Expected**: the shop's parcel reads shipped with that reference; the seller's parcel on the same order
is still waiting, with no action offered for it. An order holding only sellers' goods shows "each seller
ships their own" and no action.

## Scenario 3 — Settle a seller (US2, SC-002)

Open `/admin/payouts`, choose the seller, confirm in the dialog.

**Expected**: the dialog names the shop and the amount and **closes** on confirm (#83); a toast reports the
amount actually recorded (the pull request's read "Đã ghi nhận 1.716.000 ₫"); the seller leaves the list.
Record the same seller again from a stale tab: the server's 409 is shown in its own words, not hidden
behind the dialog.

## Scenario 4 — Not drawn for others (US3)

Sign in as a customer and as a seller. **Expected**: no console link; navigating to `/admin` directly is
turned away by `RequireRole`. (Scenario 1 is what proves the server refuses.)

## Scenario 5 — A phone (edge case)

At 390 px wide, the console and the seller console show every tab with no horizontal scroll.

## Scenario 6 — The automated checks

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~FulfilmentTests"
cd ../client && npm test -- src/pages/admin-orders src/pages/admin-order src/pages/admin-payouts \
  src/services/admin src/components/layout/user-menu src/components/address/address-card
cd ../bruno && npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `Staff_read_any_order_with_its_lines_and_parcels` and
`Staff_reading_an_order_that_does_not_exist_is_404` pass; the console's client tests and
`closes the dialog once confirmed` pass; Bruno's `staff read any order` and the two 403 checks pass. At
the merges: Order 140, client 143 (#82) then 145 (#83), Bruno 114/114.
