# Implementation Plan: Pay with VNPay (sandbox)

**Branch**: `feat/289-vnpay-sandbox` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #289

## Summary

Payment gains a second provider behind the existing seam. The stub decides at once; VNPay **opens a checkout** and
decides when the gateway's IPN arrives.
- A new table `payment_checkouts` holds what is waiting.
- The IPN handler writes the one payment row and publishes `PaymentProcessedEvent` or `PaymentFailedEvent` in the same
  transaction, as the stub path does.
- The saga does not change: it already waits for a reply, with a timeout.

A small simulator project speaks VNPay's protocol for development and CI. The storefront offers the pay link and a
return page.

## Technical Context

**Language/Version**: C# / .NET 10; React 19 + TypeScript
**Primary Dependencies**: `System.Security.Cryptography.HMACSHA512`; MassTransit outbox; TanStack Query
**Storage**: Payment's PostgreSQL: `payment_checkouts` (new) and `payments.ProviderReference` (new, nullable)
**Testing**:
- Payment.Tests on real PostgreSQL: signing, the checkout, the IPN cases, concurrency, ownership;
- Vitest for the storefront;
- `verify-saga.sh` with a `vnpay` scenario in CI's saga job;
- Playwright through the simulator, locally.
**Target Platform**: Linux containers; `dotnet run` in CI's saga job
**Project Type**: microservice change + a tool project + storefront
**Constraints**:
- the saga and the contracts are unchanged;
- the stub stays the default;
- no secret is committed;
- expand-only migration.
**Scale/Scope**: one service, one simulator, one page and a button, CI scenario, docs and ADR-004

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Payment owns the checkout and the gateway conversation. Order and the saga learn the outcome only through the existing events. The storefront asks Payment directly for the pay link, through the gateway. |
| **II. Clean Architecture Layering** | **Pass.** Signing and URL building live in Infrastructure behind Application interfaces (`IPaymentGateway`, `IVnPay`). Handlers stay in Application; the IPN controller only sends a command. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and the heart of the design.** The IPN claims the checkout with a guarded `UPDATE ... WHERE "CompletedAt" IS NULL` and inserts the payment (unique on `OrderId`). It stages the event and audit before the one save, in one transaction. A replay finds it confirmed (`02`) and writes nothing. |
| **IV. Identity Comes From the Token** | **Pass.** The checkout read takes the owner from `ICurrentUser`, and someone else's order is a 404. The IPN identifies nobody: its authority is the signature. |
| **V. Evidence Over Assumption** | **Planned.** Real-PostgreSQL tests for every IPN case and the concurrent IPN. The simulator, written separately from Payment's signer, refuses a wrongly signed URL. A CI scenario pays and cancels through it. Mutations on the signature check, the amount check and the claim. The real sandbox is recorded as not exercised. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/143-vnpay-sandbox/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
docs/architecture/adr-004-redirect-payment-gateway.md
```

### Source Code (repository root)

```text
server/src/Services/Payment/
  Domain/Entities/{PaymentCheckout.cs, Payment.cs (+ProviderReference)}
  Application/Common/Interfaces/{IPaymentGateway.cs (reshaped), IVnPay.cs, IPaymentCheckoutRepository}
  Application/Payments/ChargeOrder/…          opens a checkout when the gateway says so
  Application/Payments/Checkout/GetPaymentCheckout/…   owner's read, signed URL
  Application/Payments/VnPay/ConfirmVnPayPayment/…     the IPN
  Infrastructure/Gateway/{VnPayGateway.cs, VnPaySignature.cs, VnPayOptions.cs, StubPaymentGateway.cs}
  Infrastructure/Migrations/<ts>_AddPaymentCheckouts
  WebApi/Controllers/{PaymentCheckoutsController.cs, VnPayController.cs}
server/src/Tools/Ecommerce.VnPaySimulator/      pay page, IPN call, redirect
server/tests/Ecommerce.Payment.Tests/VnPay*Tests.cs
client/packages/core/src/{services,hooks}/payment, apps/storefront/src/pages/{order,payment-return}
.github/scripts/verify-saga.sh (vnpay scenario), .github/workflows/ci.yml (third Payment restart)
bruno/payment/, bruno/security-checks/
```

## Complexity Tracking

| Addition | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| A simulator project | CI and development must run the flow end to end, with no merchant account or network | Calling the IPN from tests alone never proves the pay URL is signed the way a gateway checks it |
| A checkout table beside `payments` | A payment row is immutable and records a decision, and a waiting checkout is not one | A `Pending` payment status would be a value a rolled-back image cannot parse, and would make "immutable once written" false |
