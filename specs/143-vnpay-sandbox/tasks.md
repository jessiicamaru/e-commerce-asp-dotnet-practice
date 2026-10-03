---
description: "Task list for Pay with VNPay (sandbox)"
---

# Tasks: Pay with VNPay (sandbox)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Payment (US1-US3)

- [ ] T001 `PaymentCheckout` entity and configuration; `Payment.ProviderReference`; migration `AddPaymentCheckouts`
- [ ] T002 `IPaymentGateway` reshaped (decide or await the customer, `MovesMoney`, `HealthOutcome`); stub unchanged in behaviour
- [ ] T003 VNPay options, signature (sort, encode, HMAC-SHA512, fixed-time compare), pay URL; startup validation (secrets, window vs saga timeout)
- [ ] T004 `ChargeOrderCommandHandler` opens a checkout (idempotent), and refuses a non-VND order
- [ ] T005 `GetPaymentCheckoutQuery`: owner-scoped state and a freshly signed URL
- [ ] T006 `ConfirmVnPayPaymentCommand`: the IPN, every code, a guarded claim plus the payment, event and audit in one save
- [ ] T007 Controllers: `PaymentCheckoutsController` (signed in), `VnPayController` (anonymous); endpoint access declared
- [ ] T008 Health `movesMoney`; startup warning for the sandbox
- [ ] T009 Tests on real PostgreSQL: signing vector, checkout, IPN codes, replay, concurrency, non-VND, ownership, health

## Phase 2: The simulator (US4)

- [ ] T010 `server/tools/Ecommerce.VnPaySimulator`: pay page, complete (IPN + redirect), own signing; slnx, Dockerfile, compose (app overlay; profile in prod)

## Phase 3: Storefront (US1)

- [ ] T011 `Payment.checkout`, `usePaymentCheckout` (polls while waiting), `movesMoney` in the payment notice
- [ ] T012 Order page: Pay with VNPay while awaiting; `/payment/vnpay-return`; vi/en; tests

## Phase 4: End to end

- [ ] T013 `verify-saga.sh` scenario `vnpay` (pay and cancel through the simulator); CI's saga job runs it as a third branch
- [ ] T014 Bruno: the checkout read, an IPN with a bad signature (97)
- [ ] T015 Playwright through the simulator, locally
- [ ] T016 Mutations: the signature check, the amount check, the claim guard, the ownership check

## Phase 5: Docs

- [ ] T017 ADR-004; `docs/features/shopping-and-checkout.md`; reference regenerated; CLAUDE.md; `.env.example`, `production.env.example`; timeline; backlog
- [ ] T018 Merged, closes #289

## Evidence

(Filled in when the work is verified.)
