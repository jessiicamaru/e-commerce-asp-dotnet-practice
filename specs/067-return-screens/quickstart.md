# Quickstart: Validating the return screens

> Written on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/README.md](contracts/README.md)

## Prerequisites

```bash
cd client
npm ci
```

For the live scenarios, the whole stack with the storefront container:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # storefront on :8088
```

---

## Scenario 1 - The rules and the pages (FR-001 to FR-006, SC-002, SC-004)

```bash
cd client
npm test -- src/utils/order/returns.test.ts src/pages/order src/pages/shop-sale src/pages/admin-order \
            src/pages/admin-returns src/services/order src/services/admin src/layouts/admin-layout
npm test          # the whole suite - 371/371 in 61 files at merge
npm run lint && npx tsc -b
```

**Expected**: `returns.test.ts` (9) - the window closes exactly 7 days after it opened; a request only for a delivered
parcel inside the window with no return; escalation and send-back only within the window of the decision; a seller
answers a request and receives what was sent back, nothing else; staff give the final word on anybody's escalated
return and act for the shop's own parcel only. Page tests: a reason asked before sending, a 409 shown in the server's
words, the refund in the order's currency, each parcel of a multi-parcel order returned on its own card, the seller
offered nothing on an escalated return, "Reject for good" on an escalation, the queue ignoring an unknown state in the
address, "Returns" in the menu for an administrator and not a moderator.

---

## Scenario 2 - The API path the screens take (SC-001)

Run Bruno against the storefront's nginx rather than the gateway, so every request takes the `/api` path the pages use:

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "baseUrl=http://localhost:8088" \
  --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: 215/215 requests and 352/352 tests at merge, including the return round trip on the shop's parcel in
`admin-audit/`.

---

## Scenario 3 - The deep link and the bundle

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8088/admin/returns        # 200
```

**Expected**: 200 - nginx serves the app for a deep link. The PR also checked that the served bundle contains
`/return/escalate`, `/return/sent` and `/orders/returns?status=` and the words in both languages; the exact command used
is not recorded.

---

## Scenario 4 - By hand in a browser (not done at merge)

Open `http://localhost:8088`, sign in as a customer with a delivered parcel of the shop's own goods, and return it from
`/orders/:id`; sign in as an administrator and take it through `/admin/returns` → the order → Accept → (as the customer)
"I've sent it back" → Mark as received. **Expected**: each state worded as in the spec, the refund shown in the order's
currency at the end. The PR says plainly that this click-through was **not done**.
