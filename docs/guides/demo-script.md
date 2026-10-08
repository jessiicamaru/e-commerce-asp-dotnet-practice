# Demo script

A walk through the shop for a presentation: start it, fill it, then follow a product from a seller's application to a
customer's review, through every role, ending on the operational views. Allow about 30 minutes. Every page named
here is a route in the storefront (`http://localhost:8088`) or the back office (`http://portal.localhost:8089`).

## Before the audience arrives

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build --wait
# a catalogue of real products in six verticals, prices in dong and dollars, Vietnamese and English text
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py
python seed/two_factor.py          # the administrator's current code, and a link to enrol an authenticator app
```

`ADMIN_EMAIL` and `ADMIN_PASSWORD` are the first administrator's, from `server/.env`. Have these open in tabs:

| Tab | Address | For |
| :-- | :-- | :-- |
| Storefront | http://localhost:8088 | shoppers and sellers |
| Back office | http://portal.localhost:8089 | staff |
| Mailpit | http://localhost:8025 | every email the shop sends |
| Seq | http://localhost:5380 | one order across every service |
| Grafana | http://localhost:3000 | the dashboard |
| RabbitMQ | http://localhost:15672 | the queues, for the resilience part |

Use **two browser profiles** (or one normal and one private window) so a customer and a seller can be signed in at
once. Cookies ignore the port, so the storefront and the back office share nothing only because they are different
hosts.

## 1. The shopper browses (3 min)

1. Open the storefront. Switch the language (Vietnamese ⇄ English) and the currency (₫ ⇄ $). Product text,
   category names and prices change. **A product with no price in dollars shows no price**: it is never
   converted ([specs/022](../../specs/022-multi-currency-prices/)).
2. Search `may anh`, with no diacritics: it finds "máy ảnh" ([specs/074](../../specs/074-search-index/)). Filter by
   price and "in stock only".
3. Open a product (`/products/:id`). Choose a variant: the price, the stock state and the photograph follow the
   variant ([specs/020](../../specs/020-product-variants/), [specs/032](../../specs/032-variant-images/)).

## 2. A seller opens a shop (5 min)

In the second profile:
1. **Sign up** (`/sign-up`) and confirm the address from the email in **Mailpit**. Selling needs a confirmed address
   ([specs/063](../../specs/063-email-confirmation/)).
2. **Open a shop** (`/open-shop`): a name and a description. It is now an **application**, waiting for staff
   ([specs/044](../../specs/044-shop-applications/)).

In the back office, as the administrator: sign in with the code from `two_factor.py`. A storefront session never
carries staff powers ([specs/110](../../specs/110-staff-two-factor/), [ADR-003](../architecture/adr-003-storefront-and-back-office.md)).
1. **Shops** (`/shops`): approve the application. The seller is told, in the bell and by email.
2. **Users** (`/users`): find someone and grant **Moderator**. Show that a moderator's lock is capped at 30 days, while
   an administrator can ban.

Back as the seller (the session renews and `/shop` appears):
1. **List a product** (`/shop/products/new`), add a photograph, set a price in dong and in dollars, and set its
   stock.
2. It is **not on sale yet**: nothing a seller lists is, until a moderator looks
   ([specs/045](../../specs/045-product-review/)).

## 3. Moderation (3 min)

In the back office:
1. **Moderation** (`/moderation`): the queues and the moderator's own decisions.
2. **Products** (`/products`): approve the seller's product. It is on the shelf in seconds.
3. Mention **Reports** (`/reports`), **Reviews** and **Questions**: hiding content tells its author, and every
   decision is in the person's history ([specs/100](../../specs/100-moderation-history/)).
4. Edit the approved product's name as the seller. It goes **back to review and off the shelf**, while prices and
   stock do not.

## 4. The customer buys (5 min)

As a customer (the first profile, signed up the same way):
1. Add the seller's product and one of the shop's own seeded products to the **cart** (`/cart`).
2. **Checkout** (`/checkout`): an address (`/addresses`), a delivery option with its days, and a voucher if one is
   shown.
   - The quote is priced by the same code as the order, so what is shown is what is charged
     ([specs/012](../../specs/012-order-totals/)).
   - Tax is on the discounted price, half away from zero.
3. **Place the order.** With the stub, it is **Paid** within seconds, and the card says no money is moved. To show
   VNPay instead, restart Payment with `PAYMENT_PROVIDER=VnPay`: the customer pays on the simulator's page and comes
   back to an order that the gateway's **signed** call settled ([specs/143](../../specs/143-vnpay-sandbox/)).
4. Show the confirmation email in Mailpit, in the order's language.

## 5. Fulfilment, delivery, review, return (5 min)

1. **The seller ships their part** (`/shop/sales/:id`): preparing, then shipped with a tracking reference.
   - The administrator ships **the shop's** part from the back office (`/orders/:id`).
   - **One order, two parcels**: each seller ships their own ([specs/035](../../specs/035-seller-shipments/)).
2. **The customer confirms a parcel received** (`/orders/:id`). Unconfirmed parcels are taken as delivered after 7
   days.
3. **Review** the product: only somebody who received it can ([specs/046](../../specs/046-product-reviews/)).
4. **Return** the other parcel: request it, the seller accepts, the customer sends it back with a reference, and the
   seller marks it received. Payment records the refund and Inventory restocks
   ([specs/066](../../specs/066-parcel-returns/)).

## 6. The money (3 min)

1. **Seller insights** (`/shop/insights`): revenue before tax and less returns, top products, and views.
2. **Administrator overview** (`/overview`): revenue per currency, never added across currencies
   ([specs/047](../../specs/047-admin-insights/)).
3. **Payouts** (`/payouts`): what the shop owes each seller.
   - That is goods less commission, plus a share of delivery, and only once a parcel can no longer come back.
   - Record one payout. A second click claims nothing: one statement decided it
     ([specs/037](../../specs/037-seller-payouts/)).

## 7. Under the hood (5 min)

1. **Seq**: query `OrderId = '<the order's id>'`. Every service's log lines for that order appear, and **Trace**
   shows the whole checkout: HTTP to the gateway, gRPC to Cart, Identity and Catalog, then the saga across the broker
   ([observability](observability.md)).
2. **Grafana**, "E-commerce overview":
   - orders paid, failed and waiting;
   - settle-time percentiles;
   - each outbox's backlog;
   - requests, latency and errors per service;
   - messages per type.
3. **Resilience, live** (optional, about 3 min). Run:

   ```bash
   ./loadtest/fault.sh broker
   ```

   It stops RabbitMQ under steady checkouts. In Grafana, Order's outbox climbs into the hundreds while the broker is
   down, then drains to zero. The run ends with every order paid and the stock exact ([evaluation §4](../testing/evaluation.md)).
4. **Audit** (`/audit`): every change in the demo, who made it, and a field-level diff.

## Afterwards

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/clean-test-debris.py --yes   # keeps the seeded products
```

Questions that tend to come up, and where the answer is:
- "What if two people buy the last one?": [evaluation §2](../testing/evaluation.md).
- "What if a service dies mid-checkout?": [evaluation §4](../testing/evaluation.md).
- "Why two apps?": [ADR-003](../architecture/adr-003-storefront-and-back-office.md).
- "How is it deployed?": [deployment](deployment.md).
