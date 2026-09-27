# Implementation Plan: A mistyped tracking reference can be corrected

**Branch**: `105-correct-tracking` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #212

## Summary

A shipped, undelivered part's tracking reference can be corrected: by its seller, or by staff for the shop's part. The
change runs under the order's row lock with a guarded update that leaves `ShippedAt` alone. It is audited with both
references, and the buyer is told.

## Technical Context

- **Order**:
  - `OrderRepository.TryCorrectTrackingAsync`, which reuses the lock and the summary routine.
  - `Orders/Commands/CorrectTracking/CorrectTracking.cs` (new).
  - Two routes.
  - `OrderNotices.TrackingCorrectedAsync`.
- **Shared**: `NotificationKind.TrackingCorrected` and `notification-kinds.json`.
- **Storefront**:
  - `Order.correctSaleTracking` and `Admin.correctShopTracking`.
  - A `CorrectTracking` dialog, shown in the seller's sale page and the admin order page.
  - Words in en and vi for seller, admin, orders and notifications.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit outbox (the notice and audit); TanStack Query

**Storage**: none new

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Order (5059), the storefront

**Performance Goals**: one locked transaction per correction

**Constraints**: `ShippedAt` never moves; nothing is changed after delivery

**Scale/Scope**: 2 routes, 1 notice kind, 1 dialog

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order only. |
| **II. Clean Architecture Layering** | **Pass.** Rules in Application, the locked transaction in the repository, and routes that only send. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The update, the audit entry and the notice are staged in one transaction. A repeat of the same reference changes nothing and publishes nothing. |
| **IV. Identity Comes From the Token** | **Pass.** The seller is the token's subject, and another's part is a 404. |
| **V. Evidence Over Assumption** | **Pass.** `TrackingCorrectionTests` (3) against a real database, 3 client tests; four mutations each caught (the delivered check, `ShippedAt` reset, untrimmed reference, the button after delivery); Order 302/302, client 531; Bruno 321/321 through rebuilt containers. |

**Post-design re-check** (after implementation): no violations.

## Project Structure

### Documentation (this feature)

```text
specs/105-correct-tracking/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # The guarded update; the notice
├── quickstart.md
├── contracts/
│   └── http-api.md      # the two routes, the notice
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/fulfilment-and-delivery.md` (the "cannot be changed" line),
`docs/features/audit-and-notifications.md`, CLAUDE.md, backlog, timeline, and a run of `generate_reference.py`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
