# Quickstart: Validating A Total With Something Behind It

> Written on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

Each scenario names the success criterion it proves. The results quoted are the ones the pull request
recorded; where a scenario was not run, it says so.

---

## Prerequisites

```bash
cd server
docker compose up -d                                   # the databases and RabbitMQ
dotnet ef database update --project src/Services/Order/Ecommerce.Order.Infrastructure/ \
                          --startup-project src/Services/Order/Ecommerce.Order.WebApi/
./start-dev.sh                                         # or start-dev.ps1; the gateway on :5000
```

`server/.env` needs `DB_USER`, `DB_PASSWORD`, `ADMIN_EMAIL` and `ADMIN_PASSWORD`. The Order service
reads its rates from `appsettings.json` (`Tax:DefaultRate` 0.10; `Tax:Rates` VN 0.10, GB 0.20, DE 0.19,
US 0.00); an environment variable such as `Tax__Rates__VN` overrides one.

A shorthand for the order database:

```bash
ORDER_SQL() { docker exec -i ecommerce-order-db psql -U "$DB_USER" -d ecommerce_order_db -c "$1"; }
```

---

## Scenario 1 - The arithmetic, and the half cent (SC-005, US4)

```bash
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~OrderTotalsTests|FullyQualifiedName~TotalsPersistenceTests"
```

**Expected**: all pass. `OrderTotalsTests` covers 0.025 → 0.03 (away from zero, not to even), three
lines of 0.25 at 10% → 0.09 of tax (per line) rather than 0.08 (on the sum), a zero rate giving 0.00,
the parts always summing, and rates of −0.01 and 1.00 refused.

**Negative control** (recorded in the pull request): change `OrderTotals.Rounding` to
`MidpointRounding.ToEven` → 2 of the 6 arithmetic tests fail. Restore it afterwards.

## Scenario 2 - Two destinations, one cart (SC-002, US2)

Covered by `TotalsPersistenceTests.The_same_cart_to_two_destinations_has_equal_subtotals_and_different_tax`
(scenario 1's command). **Expected**: 3 × 9.99 standard delivery - VN at 10% → tax 3.50, GB at 20% →
tax 6.99, subtotals equal. `An_unlisted_destination_gets_the_default_rate_and_says_so` checks that an
address in FR, which has no rate of its own, gets the default: `taxRate` 0.10.

By hand: run the Bruno `order/checkout` request twice, once with an address in `VN` and once in `GB`,
and compare `subtotal` and `taxTotal` in the two responses.

## Scenario 3 - Payment takes exactly the stored total (SC-003, US3)

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: the script passes `charged the catalogue price plus delivery plus tax at <rate>` and
`subtotal + delivery + tax - discount = total`. It recomputes the tax itself in Python `Decimal` with
`ROUND_HALF_UP` from the rate the order stored, rather than reading Order's figure. The pull request's
run: 29.97 + 15.00 + 3.00 + 1.50 = **49.47**, and Payment was asked for 49.47.

## Scenario 4 - The parts in the database add up, for every order (SC-001, FR-011)

```bash
ORDER_SQL 'SELECT count(*) AS without_parts FROM orders WHERE "Subtotal" IS NULL;'
ORDER_SQL 'SELECT count(*) AS not_summing FROM orders
           WHERE "Subtotal" + COALESCE("ShippingPrice",0) + "TaxTotal" - "DiscountTotal" <> "TotalAmount";'
ORDER_SQL "SELECT conname FROM pg_constraint WHERE conrelid = 'orders'::regclass AND contype = 'c';"
```

**Expected**: `0`, `0`, and the list includes `CK_orders_parts_sum_to_total`,
`CK_orders_no_discount_yet` and `CK_orders_tax_rate_range`. The pull request recorded 70 orders, none
without parts, none failing to add up, all three constraints present.

## Scenario 5 - The database refuses a total that does not add up (SC-001, US4)

```bash
ORDER_SQL 'INSERT INTO orders ("Id","UserId","Status","TotalAmount","CreatedAt","UpdatedAt",
                               "Subtotal","ShippingPrice","TaxTotal","DiscountTotal","TaxRate")
           VALUES (gen_random_uuid(), gen_random_uuid(), '"'"'Submitted'"'"', 99, now(), now(),
                   10, 5, 1.50, 0, 0.10);'
```

**Expected**: `ERROR: new row for relation "orders" violates check constraint
"CK_orders_parts_sum_to_total"`. The same refusal is automated in
`TotalsPersistenceTests.The_database_refuses_a_total_whose_parts_do_not_add_up`. (If the schema has
moved on since, the insert may also need columns later features made NOT NULL; the constraint name in
the error is what matters.)

## Scenario 6 - A rate changed later does not change a placed order (SC-004, FR-009)

1. Place an order to a VN address (Bruno `order/checkout`) and note `taxRate`, `taxTotal`, `totalAmount`.
2. Restart Order with `Tax__Rates__VN=0.05`.
3. `GET /api/orders/{id}` as the same customer.

**Expected**: the three figures are unchanged, because they were stored and nothing recomputes them.
No automated test changes a rate after an order is placed; whether this was run by hand is not
recorded.

## Scenario 7 - A misconfigured rate stops the service (FR-010)

```bash
Tax__DefaultRate=1.5 dotnet run --project src/Services/Order/Ecommerce.Order.WebApi/
```

**Expected**: Order does not start; the log says `Tax rate for 'default' is 1.5; it must be at least 0
and below 1.` A missing `Tax:DefaultRate` fails the same way with `Tax:DefaultRate is not configured`.
Whether this was run by hand is not recorded; the range rule itself is covered by
`OrderTotalsTests.An_impossible_rate_is_refused`.

## Scenario 8 - The Bruno collection

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `order/checkout` passes `the parts of the total add up, with tax at the destination's rate`.
At the merge the collection ran 50/51; the one failure was the known #28 (duplicate registration),
unrelated to this feature.
