# Implementation Plan: Checkout says how payment works and how long delivery takes

**Branch**: `feat/253-checkout-payment-delivery-time` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #253

## Summary

Two nullable columns on Order's `delivery_options` carry an estimate in business days, seeded from configuration,
edited at `/admin/delivery` and returned wherever an option is offered. Checkout shows it under each option and adds a
Payment card whose "no money is moved" line is read from Payment's `/health`.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19
**Primary Dependencies**: EF Core, FluentValidation; TanStack Query
**Storage**: Order's PostgreSQL - one expand-only migration (two nullable columns and a CHECK)
**Testing**: xUnit against PostgreSQL; Vitest; Bruno
**Target Platform**: Order, the storefront
**Project Type**: microservices + web client
**Performance Goals**: none new - the option rows are already read once per request
**Constraints**: a rolled-back image must still read and write the table (nullable, no default needed)
**Scale/Scope**: one table, three responses, two pages

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order owns its options; the storefront asks Payment about Payment through Payment's own public health route - no service calls another. |
| **II. Clean Architecture Layering** | **Pass.** Columns in the entity and configuration, rules in the command validator, seeding in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The estimate is saved with the option and its audit entry in the one existing save; no message. |
| **IV. Identity Comes From the Token** | **Pass.** Editing stays Admin-only by the route; nothing new is read from a body about who the caller is. |
| **V. Evidence Over Assumption** | **Planned.** Order tests against PostgreSQL (the CHECK included), Vitest both ways for the stand-in line, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/134-checkout-payment-delivery-time/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Domain/Entities/Delivery.cs
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/DeliveryConfiguration.cs
server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/<new>_DeliveryEstimate.cs
server/src/Services/Order/Ecommerce.Order.Infrastructure/Shipping/{ConfiguredShippingOptions,StoredDelivery}.cs
server/src/Services/Order/Ecommerce.Order.Application/Delivery/DeliveryFeatures.cs
server/src/Services/Order/Ecommerce.Order.Application/Orders/...ShippingOptionResponse, GetShippingOptions, quote
server/tests/Ecommerce.Order.Tests/DeliveryEstimateTests.cs
client/src/components/checkout/{delivery-choice,payment-card}
client/src/pages/{checkout,admin-delivery}
bruno/order/
```

## Complexity Tracking

No violation to justify.
