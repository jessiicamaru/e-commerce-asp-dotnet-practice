# Implementation Plan: Two Price Lists, Not One Price Converted

**Branch**: `022-multi-currency-prices` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

## Summary

Give every amount in the system a currency, and give every variant a price list per currency that an
administrator sets. Convert nothing, ever, at runtime.

- **Catalog** stores a price per currency on the variant, answers reads and gRPC in the requested
  currency, and reports a variant with no price in it as **not sellable** rather than falling back.
- **Order** freezes the currency on the order, prices delivery per currency, rounds tax to the
  currency's minor unit, and carries the currency to the saga.
- **Payment** records the currency of the amount it charged.
- **The storefront** picks a currency separately from the language and formats with both.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript 6 + React 19
**Primary Dependencies**: EF Core + Npgsql, MediatR, MassTransit, Grpc.AspNetCore, `Intl.NumberFormat`
**Storage**: one new Catalog table, one nullable column each on Order and Payment. Additive throughout
**Testing**: xUnit against real PostgreSQL (Catalog, Order, Payment); Bruno; `verify-saga.sh`
**Constraints**: an earlier image of every service keeps running; no existing amount changes value
**Scope**: Catalog, Order, Payment, Orchestrator, `Ecommerce.Shared`, `Ecommerce.Contracts`, one
proto, the storefront

## Constitution Check

| Principle | Check | Result |
| :-- | :-- | :-- |
| I. Service autonomy | Catalog owns prices; Order owns what it froze; Payment owns what it charged. The currency travels as a request value and as a message field, never as shared state. | Pass |
| II. Clean Architecture | `VariantPrice` is an entity in Catalog.Domain, the use cases are in Application, the EF mapping in Infrastructure. `IRequestCurrency` is an abstraction in `Ecommerce.Shared` over `HttpContext`, exactly as `IRequestLanguage` and `ICurrentUser` are. | Pass |
| III. Atomic writes, idempotent messaging | No new publisher and no new consumer. A price write is one row, upserted. Two existing message records gain an optional field. | Pass |
| IV. Identity from the token | Unchanged. Setting a price is an Admin action by role. | Pass |
| V. Evidence over assumption | A negative control per story, including one that removes the currency from the saga relay - the failure this project has already had once and could not detect downstream. | Pass |
| Schema evolution | One new table, two nullable columns, no narrowing, no rename. The money columns stay `decimal(18,2)` rather than narrowing for VND (research D5). | Pass |
| Invariants in the database | Unique `(VariantId, Currency)`; `Amount >= 0` as a CHECK; the order's existing parts-sum CHECK still holds under minor-unit rounding. | Pass |

No violations, so Complexity Tracking is empty.

**One thing is recorded rather than solved**: `decimal(18,2)` is two decimal places wider than VND
needs, and narrowing it would strand an earlier image. Recorded in research D5.

**One thing is a deliberate breaking change**: `price` in a product or variant response becomes
nullable, because a variant can now have no price in the currency being asked about. Returning `0`
would be a lie that reads as a free camera. The storefront is changed in the same PR; the Bruno
collection asserts the null case.

## Project Structure

```text
server/src/BuildingBlocks/
  Ecommerce.Shared/Money/                   new: IRequestCurrency, RequestCurrency, CurrencyOptions,
                                            Currency, AddRequestCurrency() + UseRequestCurrency()
  Ecommerce.Contracts/Order/OrderSubmittedEvent.cs      + Currency (additive, defaulted)
  Ecommerce.Contracts/Payment/ProcessPaymentCommand.cs  + Currency (additive, defaulted)
  Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto + currency on both requests and the reply

server/src/Services/Catalog/
  Domain/Entities/VariantPrice.cs
  Application/Products/Prices/              new: upsert + remove, per variant per currency
  Application/Products/Common/              responses carry `currency`; price becomes nullable
  Infrastructure/Migrations/AddVariantPrices (+ seeds the USD list at a stated rate)
  Infrastructure/.../ProductRepository      loads prices for the requested currency
  WebApi/Controllers/ProductsController     price endpoints
  WebApi/Grpc/CatalogPricingService         answers in the requested currency, or not at all

server/src/Services/Order/
  Domain/Entities/Order.cs                  + Currency
  Application/Orders/Common/                OrderTotals gains `decimals`; CheckoutPricing resolves
                                            the currency and refuses what is not priced in it
  Application/Common/Interfaces/            ShippingOption gains a price per currency
  Infrastructure/Shipping/                  reads Shipping:Options[].Prices
  Infrastructure/Migrations/AddOrderCurrency

server/src/Services/Orchestrator/           OrderStateData + Currency; relayed to ProcessPaymentCommand
server/src/Services/Payment/                payments.Currency; the consumer stores what it was sent

client/src/
  config/money/, components/layout/currency-switcher/, utils/shared/money.ts
bruno/                                      a USD read, a price write, the unpriced-variant refusal
```

## Order of work

1. `Ecommerce.Shared` currency resolution and the `Currency` value - everything else needs it.
2. Catalog: the table, the seeded USD list, reads, admin writes, gRPC.
3. Order: the column, delivery per currency, minor-unit rounding, the language-shaped plumbing.
4. Contracts + Orchestrator + Payment: the currency reaches the row that records the charge.
5. The storefront: the switcher, the formatting, the unpriced case.
6. Evidence: tests, negative controls, Bruno, the stack, `verify-saga.sh` in both currencies.

**Step 4 is the one to get right.** It is the relay this project has already broken once, and the
failure mode here is undetectable after the fact.

## Design artifacts

[research.md](research.md) · [data-model.md](data-model.md) · [contracts/api.md](contracts/api.md) ·
[quickstart.md](quickstart.md)
