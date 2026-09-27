---
description: "Task list for Administrators manage delivery and the carrier"
---

# Tasks: Administrators manage delivery and the carrier

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the handlers - they could not compile before the delivery types existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Storage and seed (US4)

- [X] T001 Entities `DeliveryOption`, `DeliveryOptionPrice`, `Carrier`; mappings; migration `DeliverySettings`
- [X] T002 [US4] `DeliverySeed` (missing codes and carrier only); run at startup after migrations; `Shipping:Carrier` in appsettings

## Phase 2: Checkout reads the table (US1, US2)

- [X] T003 [US1] [US2] `StoredShippingOptions` (scoped, offered only); `ConfiguredShippingOptions` becomes the seed's source
- [X] T004 The Order test fixture uses the real stored options and seeds them

## Phase 3: Administration (US1-US3)

- [X] T005 `DeliveryFeatures`: settings and carrier reads, option and carrier saves with validators, the last-option 409, audit
- [X] T006 Routes: `GET delivery`, `PUT delivery/options/{code}`, `PUT delivery/carrier` (Admin), `GET delivery/carrier` (anyone)
- [X] T007 `DeliverySettingsTests` (10)

## Phase 4: Storefront (US1-US3)

- [X] T008 [US3] `TrackingLink` in `order-shipments`, `parcel-actions`, `pages/order`; a default carrier in `test/setup.ts`
- [X] T009 [US1] [US2] [US3] `/admin/delivery` (carrier form, option rows, a new option), route (Admin), menu, words en/vi
- [X] T010 Tests: `TrackingLink` (3), the page (4); the order-shipments test given a query client

## Phase 5: Verification and docs

- [X] T011 Mutations (quickstart Scenario 4) - each red; Order 286/286
- [X] T012 [P] Bruno `order/` seq 14-18
- [X] T013 Rebuilt Order and the storefront; Bruno through the gateway
- [X] T014 Docs: `docs/features/shopping-and-checkout.md`, `docs/features/fulfilment-and-delivery.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T015 Merged as #205, closing #196
