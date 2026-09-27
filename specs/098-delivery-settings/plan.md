# Implementation Plan: Administrators manage delivery and the carrier

**Branch**: `098-delivery-settings` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #196

## Summary

Delivery options, their prices and the shop's one carrier move from configuration into Order's database, seeded from
configuration with missing codes only. Checkout reads the table per request through a new scoped `StoredShippingOptions`.
Administrators edit options and the carrier (`/api/orders/delivery/*`, `/admin/delivery`); the carrier's tracking template
is public, and the storefront links every shop-carrier tracking reference through one `TrackingLink` component.

## Technical Context

- Order Domain `Entities/Delivery.cs` (new); Infrastructure `Persistence/Configurations/DeliveryConfiguration.cs`,
  `OrderDbContext` (two sets), migration `DeliverySettings`, `Shipping/StoredDelivery.cs` (`StoredShippingOptions`,
  `DeliveryRepository`, `DeliverySeed`), `DependencyInjection.cs`; Application `Delivery/DeliveryFeatures.cs` (new);
  WebApi `OrdersController` (four routes), `Program.cs` (seed after migrations), `appsettings.json` (`Shipping:Carrier`)
- Storefront: `services/delivery`, `hooks/delivery`, `components/order/tracking-link` (new), used in `order-shipments`,
  `parcel-actions`, `pages/order`; `pages/admin-delivery` (new), route, menu, words; `test/setup.ts` (a default carrier)
- Tests: `Ecommerce.Order.Tests/DeliverySettingsTests.cs`, `OrderTestFixture` (real stored options, seeded); client tests

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, FluentValidation, `Ecommerce.Shared.Money`; TanStack Query

**Storage**: three new tables in `ecommerce_order_db` (5434)

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Order (5059) and the storefront

**Performance Goals**: one small query per checkout request (a handful of rows)

**Constraints**: orders keep what they froze; a restart never overwrites an edit; every instance current

**Scale/Scope**: 3 tables, 4 routes, 1 page, 1 shared component

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order owns delivery, prices checkout and now stores both; no other service involved. |
| **II. Clean Architecture Layering** | **Pass.** Rules in Application (`DeliveryFeatures`), storage and seeding in Infrastructure, routes only send. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each save stages its audit entry before the one save; the seed is idempotent (`ON CONFLICT DO NOTHING`) and safe on several instances. |
| **IV. Identity Comes From the Token** | **Pass.** Admin role on the writes; the audit's actor from `ICurrentUser`. |
| **V. Evidence Over Assumption** | **Pass.** Ten server tests over a real database; the whole Order suite (286) now prices from the seeded table; four mutations each caught; Bruno through rebuilt containers. |

The constitution's configuration rule ("a missing required setting fails at startup") still holds: the configured options
are validated at startup as the seed's source.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/098-delivery-settings/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # Three tables; the seed
├── quickstart.md
├── contracts/
│   └── http-api.md      # /api/orders/delivery/*
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context, plus `docs/features/shopping-and-checkout.md`, `docs/features/fulfilment-and-delivery.md`,
CLAUDE.md, backlog, timeline, and `generate_reference.py` (tables and routes added).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Option names in one language; a courier role or several carriers; per-seller delivery.
