# Implementation Plan: Shoppers see the vouchers they could use

**Branch**: `114-public-vouchers` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #220

## Summary

A voucher gets `IsPublic` (private by default). An anonymous `GET /api/vouchers/public` lists the live, public vouchers
of the platform and of named shops, optionally for one product, in the request's currency - without limits or counts.
The shop page, the product page and the checkout show them; checkout's "Use" goes through the existing voucher box. The
quote's lines gain the seller id so checkout knows which shops to ask about.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core + Npgsql, MediatR, FluentValidation; TanStack Query
**Storage**: Order's PostgreSQL; one column
**Testing**: xUnit against PostgreSQL, Vitest, Bruno
**Target Platform**: the compose stack and CI
**Project Type**: microservice + web storefront
**Performance Goals**: one indexed-enough query per page view (a shop has a handful of vouchers)
**Constraints**: anonymous read; no limit or count leaves
**Scale/Scope**: one column, one endpoint, one response field, three page sections

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order answers from its own table; pages pass the ids they already hold. No new edge. |
| **II. Clean Architecture Layering** | **Pass.** Query and validator in Application, the read in the repository. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The flag is written by the create and edit that already commit with their audit entries. The read writes nothing. |
| **IV. Identity Comes From the Token** | **Pass.** The read is anonymous and names no person; the flag is set by the owner from the token. |
| **V. Evidence Over Assumption** | **Planned.** Tests per filter against PostgreSQL, mutations, Bruno. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): unchanged - see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/114-public-vouchers/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/…Domain/Entities/Voucher.cs (IsPublic), …Infrastructure/Migrations (AddVoucherVisibility)
server/src/Services/Order/…Application/Vouchers/PublicVouchers.cs (query, validator, handler)
server/src/Services/Order/…Application/Vouchers/VoucherFeatures.cs, EditVoucher.cs (isPublic)
server/src/Services/Order/…Application/Orders/… (OrderItemResponse.SellerId)
server/tests/Ecommerce.Order.Tests/PublicVoucherTests.cs
client/src/components/voucher/public-vouchers/, voucher-form, voucher-edit, voucher-page
client/src/pages/shop-front, pages/product, components/checkout/voucher-box
bruno/seller/, bruno/security-checks/
```

## Complexity Tracking

No violation to justify.
