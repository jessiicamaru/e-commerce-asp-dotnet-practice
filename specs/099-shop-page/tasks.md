---
description: "Task list for A shop has a page"
---

# Tasks: A shop has a page

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included. They were written with the handlers, because they could not compile before the new commands existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The description (US2)

- [X] T001 [US2] Identity: `SellerProfile.Description` (≤ 500) and migration `AddShopDescription`
- [X] T002 [US2] `DescribeShopCommand`: validator, handler (trim or null; stage the event and the audit entry before the save), `PUT /api/sellers/me/description`; `GET /api/sellers/me` carries it
- [X] T003 [US2] `SellerDescribedEvent` in Contracts
- [X] T004 [US2] Catalog: `Seller.Description` and `DescriptionObservedAt`, migration, `TryRecordDescriptionAsync` (guarded upsert), `SellerDescribedConsumer`

## Phase 2: The shop page (US1)

- [X] T005 [US1] `GetProductsQuery.SellerId` passed to `GetPaginatedAsync`
- [X] T006 [US1] `GetShopQuery` (404 when unknown, unnamed or suspended), `CountOnShelfBySellerAsync`, `ShopsController`; gateway `catalog-shops-route`
- [X] T007 Tests: `ShopPageTests` (4) and `ShopDescriptionTests` (3)

## Phase 3: Storefront (US1, US2)

- [X] T008 [US1] `services/shops`, `pages/shop-front`, route `/shops/:sellerId`, `ProductQuery.sellerId`, `queryKeys.shopFront`
- [X] T009 [US1] Product page: the shop name links to its page (`product.soldByShop`); the shop's own goods stay as text
- [X] T010 [US2] `Seller.describe`, `useDescribeShop`, `DescribeShopDialog` and a link to the shop's page in the seller layout; words en/vi
- [X] T011 Tests: the shop page (3), the product link (2), the dialog (1)

## Phase 4: Verification and docs

- [X] T012 Mutations (quickstart Scenario 4), each red; Catalog 234, Identity 191, client 497
- [X] T013 [P] Bruno `seller/` seq 83-85 and `security-checks/` seq 61-62
- [X] T014 Rebuilt Identity, Catalog, the gateway and the storefront; Bruno 296/296; the page checked at `:8088`
- [X] T015 Docs: `docs/features/marketplace.md`, `docs/features/catalog.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T016 Merged as #206, closing #197
