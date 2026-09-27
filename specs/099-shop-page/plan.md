# Implementation Plan: A shop has a page

**Branch**: `099-shop-page` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #197

## Summary

A seller writes a description of their shop. Identity stores it on the seller profile and announces it with
`SellerDescribedEvent` in the same save, and Catalog keeps it on its `sellers` read model behind its own timestamp guard.
Catalog serves `GET /api/shops/{sellerId}` (name, description, count on the shelf; 404 for an unknown, unnamed or
suspended seller), and the public listing takes a `sellerId`. The storefront adds `/shops/:sellerId`, turns the shop name
on a product page into a link to it, and gives the seller a description dialog beside the rename.

## Technical Context

- **Identity**: `SellerProfile.Description`; its configuration; migration `AddShopDescription`; `Sellers/SellerCommands.cs`
  (`DescribeShopCommand`, its validator and handler, and `SellerProfileResponse.Description`); `SellersController`
  (`PUT me/description`).
- **Contracts**: `Identity/SellerEvents.cs` (`SellerDescribedEvent`).
- **Catalog**:
  - `Seller.Description` and `DescriptionObservedAt`; migration `AddShopDescription`.
  - `SellerRepository` (`TryRecordDescriptionAsync`, `GetAsync`) and `ProductRepository.CountOnShelfBySellerAsync`.
  - `GetProductsQuery.SellerId`.
  - `Sellers/ShopFeatures.cs` (new).
  - `ShopsController` (new) and `SellerDescribedConsumer`.
- **Gateway**: `catalog-shops-route`.
- **Storefront**:
  - `services/shops` and `pages/shop-front` (new), plus the route.
  - `ProductQuery.sellerId` and `queryKeys.shopFront`.
  - The product page's shop link (`product.soldByShop`).
  - `Seller.describe`, `useDescribeShop` and `components/seller/describe-shop-dialog` in the seller layout, with a link to
    the seller's own shop page.
  - Words in en and vi.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit (EF outbox), FluentValidation; TanStack Query, react-i18next
`Trans`

**Storage**: one column in `ecommerce_identity_db` (5435) and two in `ecommerce_catalog_db` (5433)

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Identity (5056), Catalog (5057), the gateway and the storefront

**Performance Goals**: the shop read is two indexed queries, a primary key lookup plus a count over `products.SellerId`

**Constraints**: an anonymous page must not call Identity; the shop page must apply the same shelf rule as the catalogue

**Scale/Scope**: 3 columns, 1 event, 2 routes and 1 filter, 1 page and 1 dialog

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity owns the description; Catalog keeps a read model of it through a message. No synchronous call is added, and the shop page answers with Identity down. |
| **II. Clean Architecture Layering** | **Pass.** The rules sit in Application (`DescribeShopCommand`, `ShopFeatures`), the SQL in Infrastructure repositories, and the controllers only send. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The event and the audit entry are staged before the one save. The consumer's write is a single guarded upsert, so a redelivery or an older event is a no-op (research D4). |
| **IV. Identity Comes From the Token** | **Pass.** `PUT /api/sellers/me/description` takes no seller id; the handler reads `ICurrentUser`. |
| **V. Evidence Over Assumption** | **Pass.** 4 Catalog and 3 Identity tests against real databases, and 6 client tests; six mutations each caught; Bruno 296/296 through rebuilt containers; the page checked in the running storefront. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/099-shop-page/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5
├── data-model.md        # Three columns; the guarded upsert
├── quickstart.md
├── contracts/
│   ├── http-api.md      # /api/shops, ?sellerId=, /api/sellers/me/description
│   └── messages.md      # SellerDescribedEvent
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/marketplace.md`, `docs/features/catalog.md`, CLAUDE.md, the backlog and the
timeline, and a run of `generate_reference.py` (new routes, message and columns).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
