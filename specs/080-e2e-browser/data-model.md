# Phase 1 Data Model: The storefront in a real browser

> Written on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

No table, column, index, constraint or migration changed. The feature adds a browser test suite and changes three
storefront components; no service's schema is touched, so there is nothing an earlier image could fail to read.

What it does write is **test data**, into the services' own databases, through the gateway (research D5). This page
records what one run makes and what it removes.

## What one run makes

Every name carries the run's tag, `run = Date.now()` at the start of the suite, so two runs never collide.

| What | Made by | Owning service | Values |
| :-- | :-- | :-- | :-- |
| A category | `POST /api/categories` (administrator) | Catalog | name `E2e <run>`, slug `e2e-<run>` |
| A seller | `POST /api/auth/register-seller`, confirmed with the token from Mailpit, application approved | Identity (account, shop application, seller profile, outgoing emails); Catalog (`sellers` read model) | `e2e-seller-<run>@local.test`, shop `E2e Lens House <run>` |
| Two products | `POST /api/products` (the seller) | Catalog; Inventory registers a stock row for each variant | `E2e camera <run>` and `E2e waiting <run>`, SKU `E2ECAMERA<run>` / `E2EWAITING<run>` cut to 30 characters, price `1,250,000` in the default currency |
| Approval of the camera | `POST /api/products/{id}/approve` (administrator) | Catalog | `ReviewStatus = Approved` |
| Stock | `PUT /api/stock/{variantId}` (the seller) | Inventory | `QuantityOnHand = 5` |
| A customer and an address | `POST /api/auth/register`, `POST /api/addresses` | Identity | `e2e-customer-<run>@local.test`; *E2e Customer*, 12 Ly Thuong Kiet, Ha Noi, 100000, VN |
| A moderator | `POST /api/auth/register`, `PUT /api/users/{id}/roles/Moderator` (administrator) | Identity | `e2e-moderator-<run>@local.test` |

The browser flows then make, through the pages:

| What | Owning service |
| :-- | :-- |
| Approval of the waiting product, by the moderator | Catalog (plus an audit entry in Activity) |
| A cart line, then an order: its lines, parcel, reservation, payment | Cart, Order, Inventory, Payment, the saga's instance in the Orchestrator |
| The parcel prepared, shipped with `VNPOST-E2E-1`, and delivered by the customer | Order; Catalog's `review_eligibility` from `ParcelDeliveredEvent` |
| A 5-star review | Catalog (`reviews`, and the product's rating) |
| Notices, audit entries and emails the steps cause | Activity, Identity |

Every account signs in with the one password `E2e-Passw0rd!1` (a test constant in `e2e/support/api.ts`).

## What one run removes

`afterAll` deletes, as the administrator, each product the run listed and then its category (research D7). Deleting
a product announces `ProductDeletedEvent`, so Inventory drops its stock rows; the review goes with it, because
`reviews` cascades on the product's key.

Left behind, on purpose or for want of a way: the three accounts, the seller's shop and application, the address,
the order with its parcel, reservation and payment, and the notices, audit entries and emails. The order keeps the
product's name, price and seller frozen on its lines (specs/009, specs/034), so it still reads correctly with the
product gone.
