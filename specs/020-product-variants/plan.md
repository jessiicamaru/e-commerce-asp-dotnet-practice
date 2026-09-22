# Implementation Plan: Product Variants

**Branch**: `020-product-variants` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

## Summary

The variant becomes the sellable unit: it carries the SKU, the price, the options, the stock and the
availability. Catalog gains two tables and two gRPC methods; Cart, Order and Inventory key by variant;
the storefront makes the customer choose. Every existing product gets one variant **whose id is the
product's own id**, which is what makes four databases correct with no data migration.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript 6 + React 19
**Primary Dependencies**: EF Core + Npgsql, MediatR, FluentValidation, MassTransit, Grpc.AspNetCore, TanStack Query
**Storage**: PostgreSQL per service. Two new tables in Catalog; one new column in Cart; three in Order; none in Inventory
**Testing**: xUnit against real PostgreSQL in all four services; Bruno; `verify-saga.sh` must pass unchanged
**Target Platform**: Linux containers
**Constraints**: additive schema only; older images of all four services must keep running; one currency (the next feature adds the second)
**Scope**: 4 services, 2 protos, 2 message contracts, the client

## Constitution Check

| Principle | Check | Result |
| :-- | :-- | :-- |
| I. Service autonomy | Catalog owns variants and prices; Inventory owns stock by sellable-unit id; neither reaches into the other. Two new gRPC methods, both read-only. | Pass |
| II. Clean Architecture | Variant entity in Catalog.Domain, use cases in Application, EF mapping in Infrastructure, controllers only `Mediator.Send`. | Pass |
| III. Atomic writes, idempotent messaging | No new publisher. The availability consumer keeps its guarded `UPDATE`, now per variant, and recomputes the product's flag in the same transaction. | Pass |
| IV. Identity from the token | No user id anywhere new. Admin endpoints by role. | Pass |
| V. Evidence over assumption | Negative controls planned per service; the upgrade path is exercised against a populated database, not reasoned about. | Pass |
| Schema evolution | Two new tables, four new nullable columns, one backfill. Nothing dropped, renamed or narrowed. `products.Price`/`Sku` and Inventory's column names stay precisely so an earlier image runs. | Pass |
| Invariants in the database | Unique SKU per variant; unique `(VariantId, Name)`; FKs restrict/cascade. The "no two variants with identical option sets" rule is in the handler — recorded in research D5, not expressible without a trigger. | Pass, with D5 recorded |

No violation needs justifying, so Complexity Tracking is empty. The one thing the constitution would
normally frown at — Inventory's column called `ProductId` holding a variant id — is the *cheaper* side
of a rename that would break an earlier image; research D9.

## Project Structure

```text
server/src/BuildingBlocks/
  Ecommerce.Contracts/Order/OrderSubmittedEvent.cs          + OrderItemDto.VariantId
  Ecommerce.Contracts/Inventory/StockAvailabilityChangedEvent.cs   + VariantId
  Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto     + PriceVariants, DescribeVariants
  Ecommerce.Contracts.Grpc/Protos/cart_reading.proto        + CartItem.variant_id

server/src/Services/Catalog/
  Domain/Entities/ProductVariant.cs, VariantOption.cs       new
  Application/Products/Variants/                            new: AddVariant, UpdateVariant, options → summary
  Application/Products/Common/ProductResponse.cs            + priceVaries, variantCount, variants
  Application/Products/Availability/                         per-variant record + product recompute
  Infrastructure/Configurations/, Migrations/AddProductVariants
  WebApi/Controllers/ProductsController.cs                  + variant endpoints
  WebApi/Grpc/CatalogPricingService.cs                      + the two methods

server/src/Services/Cart/     line keyed by variant, display from DescribeVariants, gRPC returns variant_id
server/src/Services/Order/    order line freezes variant id, sku, option summary; checkout prices variants
server/src/Services/Inventory/ comments + the availability announcement carries the variant id
client/                        "from" price on the listing, a variant chooser on the product page
bruno/                         variant endpoints and their refusals
```

## Order of work

1. **Contracts and Catalog** — nothing else can key by a variant until variants exist.
2. **Inventory** — announcements carry the variant id; stock endpoints take one.
3. **Cart** — lines hold a variant; display uses `DescribeVariants`.
4. **Order** — checkout prices variants and freezes the words.
5. **Client**, then Bruno, then the upgrade rehearsal against a populated database.

## Design artifacts

[research.md](research.md) · [data-model.md](data-model.md) · [contracts/api.md](contracts/api.md) ·
[quickstart.md](quickstart.md)
