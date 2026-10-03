---
description: "Task list for Pay with VNPay (sandbox)"
---

# Tasks: Pay with VNPay (sandbox)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Payment (US1-US3)

- [x] T001 `PaymentCheckout` entity and configuration; `Payment.ProviderReference`; migration `AddPaymentCheckouts`
- [x] T002 `IPaymentGateway` reshaped (decide or await the customer, `MovesMoney`, `HealthOutcome`); stub unchanged in behaviour
- [x] T003 VNPay options, signature (sort, encode, HMAC-SHA512, fixed-time compare), pay URL; startup validation (secrets, window vs saga timeout)
- [x] T004 `ChargeOrderCommandHandler` opens a checkout (idempotent), and refuses a non-VND order
- [x] T005 `GetPaymentCheckoutQuery`: owner-scoped state and a freshly signed URL
- [x] T006 `ConfirmVnPayPaymentCommand`: the IPN, every code, a guarded claim plus the payment, event and audit in one save
- [x] T007 Controllers: `PaymentCheckoutsController` (signed in), `VnPayController` (anonymous); endpoint access declared
- [x] T008 Health `movesMoney`; startup warning for the sandbox
- [x] T009 Tests on real PostgreSQL: signing vector, checkout, IPN codes, replay, concurrency, non-VND, ownership, health

## Phase 2: The simulator (US4)

- [x] T010 `server/src/Tools/Ecommerce.VnPaySimulator`: pay page, complete (IPN + redirect), own signing; slnx, Dockerfile, compose (app overlay; profile in prod)

## Phase 3: Storefront (US1)

- [x] T011 `Payment.checkout`, `usePaymentCheckout` (polls while waiting), `movesMoney` in the payment notice
- [x] T012 Order page: Pay with VNPay while awaiting; `/payment/vnpay-return`; vi/en; tests

## Phase 4: End to end

- [x] T013 `verify-saga.sh` scenario `vnpay` (pay and cancel through the simulator); CI's saga job runs it as a third branch
- [x] T014 Bruno: the checkout read, an IPN with a bad signature (97)
- [x] T015 Playwright through the simulator, locally
- [x] T016 Mutations: the signature check, the amount check, the claim guard, the ownership check

## Phase 5: Docs

- [x] T017 ADR-004; `docs/features/shopping-and-checkout.md`; reference regenerated; CLAUDE.md; `.env.example`, `production.env.example`; timeline; backlog
- [x] T018 Merged, closes #289 - #298

## Evidence

- `Ecommerce.Payment.Tests`: 51/51 on real PostgreSQL, with 21 new. They cover:
  - the signature against an independent Python vector;
  - the owner's signed link (amount in hundredths, reference, merchant, locale, address, return URL, an expiry in Vietnam time equal to the checkout's);
  - one checkout per order, undecided;
  - a non-dong order refused and the saga told;
  - a signed success recording one approved payment with VNPay's number and one reply;
  - a cancellation (`24`) failing the order;
  - a repeat answering `02`, and ten concurrent copies recording one payment and one reply;
  - forged, tampered, unsigned and other-merchant notifications answering `97`, a wrong amount `04`, an unknown reference `01`;
  - someone else's checkout 404, an unknown order `Preparing` with nothing in it, paid with no link, expired with no link;
  - settings refused at startup, an unknown provider stopping the service, and the sandbox saying it moves no money;
  - a person's checkouts in their export and nobody else's.
- Mutations, 8 of 10 caught:
  - the signature not compared;
  - the merchant not compared;
  - the amount not compared;
  - success on any code;
  - the owner not checked;
  - any currency accepted;
  - the amount not in hundredths;
  - the link outliving the checkout.

  Two survive by design, as defence in depth in front of the unique index on `payments.OrderId`: the up-front already-confirmed check, and the claim's `"CompletedAt" IS NULL`. Removing either leaves every repeat answered `02` with nothing written.
- Client: 742/742 (vitest), oxlint and tsc clean. The new audit action `PaymentCheckoutOpened` was caught unlabelled by `audit-actions.test.ts`, and labelled in both languages.
- In a browser (compose with `PAYMENT_PROVIDER=VnPay`, Edge), `e2e/vnpay.spec.ts` 3/3:
  - paid at the simulator: Paid;
  - cancelled there: "Not placed";
  - a forged "paid" return address for an unknown order: "Order not found."
- Through the gateway: `/api/payment/health` reports `VnPay sandbox - no money is moved`, `movesMoney: false`, `configuredOutcome: Customer`; a forged IPN answers `{"RspCode":"97","Message":"Invalid signature"}`.
- CI: the saga job's scenarios 3 (`vnpay-pay`) and 4 (`vnpay-cancel`), see the PR.
- Not exercised: VNPay's real sandbox (needs the merchant's registration), and refunds through VNPay's API (recorded only, ADR-004).
