# Implementation Plan: Which shop each parcel comes from

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Branch**: `036-parcel-shop-names` | **Spec**: [spec.md](spec.md)

## Summary

Carry the selling shop's name from Catalog to the order, and freeze it. Catalog's `PriceVariants` and
`DescribeVariants` answers gain `optional string seller_name`, filled from its `sellers` read model in one
batched lookup per request; checkout copies it onto a new nullable column, `order_items.SellerName`. Order
lines, the checkout quote and each parcel return it; the client heads each parcel "from …" or "from the
shop" and each line "Sold by …". No backfill, no new call. Decisions in [research.md](research.md).

## Technical Context

- **Catalog**: `CatalogPricingService` takes `ISellerRepository`; `PriceVariants` and `DescribeVariants`
  look the names up once per request and set `seller_name`.
- **Order**: `order_items.SellerName varchar(100) NULL` (migration `AddOrderItemSellerName`);
  `CatalogPrice`/`PricedLine` carry it; checkout freezes it; order lines, parcels and the quote return it.
- **Client**: order lines say "Sold by …"; parcel headings name the shop, or "The shop".

**Language/Version**: C# / .NET 10; TypeScript / React 19

**Primary Dependencies**: `Ecommerce.Contracts.Grpc` (proto3 `optional`), EF Core with Npgsql, MediatR;
react-i18next in the client

**Storage**: `ecommerce_order_db` - migration `20260923122805_AddOrderItemSellerName`; Catalog reads its
existing `sellers` table

**Testing**: xUnit against real PostgreSQL (Catalog `VariantSellerPricingTests`, Order `ShopNameTests`);
Vitest (`order-lines`, `order-shipments`); Bruno; `verify-saga.sh`

**Target Platform**: Catalog gRPC on 6057 / 5157, Order on 5059, through the gateway on 5000

**Constraints**: one seller lookup per pricing request, not per line; an unknown name is null, never `""`
or an id; nothing looked up at read time

**Scale/Scope**: a handful of lines per checkout

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Order reads its own column; no new call. The name rides an answer checkout already asks for. |
| II - Clean Architecture | **Pass.** The proto field is read in Infrastructure; Application sees `string?`. |
| III - Atomic writes | **Pass.** Written in the order's own save. |
| IV - Identity from the token | **Pass.** Unchanged. Only a public display name is disclosed. |
| V - Evidence | **Pass.** Tests for: frozen at checkout, unchanged by a later rename, unknown recorded as null, one batched lookup in Catalog. |
| Schema compatibility | **Pass.** One nullable column. Additive. |

No Complexity Tracking entries.

**Post-design re-check**: unchanged. Principle I in particular: Catalog's copy of shop names is a
display read model, and here it informs nothing but a label - no sell, allow or charge decision.

## Project Structure

### Documentation (this feature)

```text
specs/036-parcel-shop-names/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D4
├── data-model.md            # order_items.SellerName
├── quickstart.md
├── contracts/
│   └── api.md               # the proto field and the HTTP fields
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto     # seller_name = 10
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs  # ISellerRepository, one lookup
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/OrderItem.cs                                    # SellerName
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/ICatalogPrices.cs                                         # CatalogPrice.SellerName
│   ├── Orders/Commands/SubmitOrder/{SubmitOrderCommand.cs,SubmitOrderCommandHandler.cs}
│   ├── Orders/Common/{CheckoutPricing.cs,OrderMapping.cs,OrderResponses.cs}
│   └── Orders/Queries/GetCheckoutQuote/GetCheckoutQuoteQuery.cs
└── Ecommerce.Order.Infrastructure/
    ├── Catalog/GrpcCatalogPrices.cs                                                # SellerNameOf
    ├── Migrations/20260923122805_AddOrderItemSellerName.cs
    └── Persistence/Configurations/OrderItemConfiguration.cs                        # max length 100
server/tests/Ecommerce.Catalog.Tests/VariantSellerPricingTests.cs                   # +3 tests
server/tests/Ecommerce.Order.Tests/ShopNameTests.cs                                 # new, 5 tests
client/src/components/order/order-lines/{index.tsx,index.test.tsx}
client/src/components/order/order-shipments/{index.tsx,index.test.tsx}
client/src/services/order/types.ts, client/src/locales/{en,vi}/orders.json
CLAUDE.md, .specify/feature.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## Verification

Order and Catalog tests; client tests; Bruno; `verify-saga.sh`; end to end with a fresh two-seller
order, then a shop rename, then the old order re-read.

**What the pull request recorded** (#80): Catalog 132 → **135**, Order 98 → **103**, all backend 357;
client 104 → **109**, lint clean; Bruno 103/103 requests, 164/164 tests; `verify-saga.sh` passed. The
Catalog name test and all five Order tests were seen red against plumbing that compiled but did not carry
the name. End to end on demo data: Lan's quote named both shops; her order's two parcels named both; Mai
renamed her shop, the check **waited until the catalogue showed the new name**, and the order still read
"Mai Lens Hà Nội" (the first version of that check could have passed before the rename reached Catalog
and was re-run properly); the name was put back afterwards. No Bruno request was added.

## What this feature does not finish

- Older orders show no shop names; there is no backfill (D4).
- A seller whose registration Catalog had not heard yet at checkout stays unnamed on that order for good.
- No contact or profile page for a seller.
