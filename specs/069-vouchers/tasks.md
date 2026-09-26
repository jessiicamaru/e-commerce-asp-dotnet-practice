---
description: "Task list for Vouchers (part 1 - the server)"
---

# Tasks: Vouchers (part 1 - the server)

> Completed on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/http-api.md](contracts/http-api.md)

**Tests**: Included and written first. Pricing is pure and tested as such; the claims and releases are the database's
guarded statements and are tested against real PostgreSQL, including races.

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 `VoucherPricing` (pure) with unit tests in `server/tests/Ecommerce.Order.Tests/VoucherPricingTests.cs`. Cover:
  - percent with a cap;
  - fixed;
  - free delivery;
  - the minimum spend;
  - the targets;
  - shop before platform;
  - stacking limits;
  - the currency rule;
  - proportional allocation with the remainder;
  - the dates, status and conditions.
- [X] T002 `OrderTotals.Compute` with discounts: tax on the discounted amounts, and the CHECK identity. Tests in `OrderTotalsTests`.
- [X] T003 The entities, configurations and migration `AddVouchers`, the `order_items` discount columns, and `IVoucherRepository`.
- [X] T004 `CheckoutPricing`, the quote and submit with codes: the lines' discounts frozen, the earnings less the shop discount, the claims in the order's transaction, and the redemptions. Integration tests in `VoucherCheckoutTests`, including the race for the last use and the per-customer limit.
- [X] T005 Release on a failed or cancelled order, idempotent.
- [X] T006 The refund of a returned parcel after discount, and seller insights after the shop discount. Tests.
- [X] T007 `VouchersController`: create, mine, disable. Validators, audit, the gateway route. Tests in `VoucherManagementTests`.
- [X] T008 Bruno, the reference, the docs, and the mutation checks.

> **Correction (backfill)**: T002's tests are not in `OrderTotalsTests` - that file was not touched by #153 (its cases
> still call `Compute` without discounts). The tax-after-discount and parts-sum checks are
> `VoucherPricingTests.Tax_is_on_the_discounted_price_and_the_parts_still_sum_to_the_total` and
> `A_discount_larger_than_its_line_is_a_programming_error_not_a_refund`, plus the checkout tests that compare quote and
> order.

## The same work by file (added in the backfill; all done in #153)

### Foundational

- [X] T009 [P] `Voucher`, `VoucherCondition`, `VoucherTarget`, `VoucherAmount`, `VoucherCustomerUse`, `VoucherRedemption` in `server/src/Services/Order/Ecommerce.Order.Domain/Entities/Voucher.cs`; enums in `.../Domain/Enums/VoucherEnums.cs`
- [X] T010 [P] `OrderItem.ShopDiscount` / `PlatformDiscount` and `Order.Vouchers` in `.../Domain/Entities/{OrderItem.cs, Order.cs}`
- [X] T011 `VoucherConfiguration.cs`, `OrderItemConfiguration.cs` (`CK_order_items_discounts`), `OrderConfiguration.cs` (`CK_orders_discount_not_negative` replacing `CK_orders_no_discount_yet`), `OrderDbContext` sets
- [X] T012 Migration `server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260926073015_AddVouchers.cs`
- [X] T013 `IVoucherRepository` in `.../Application/Vouchers/IVoucherRepository.cs`; `VoucherRepository` (`FindForCheckoutAsync`, `CustomerFactsAsync`, `ClaimAndSaveAsync`, `ReleaseForOrderAsync`, `TrySaveNewAsync`, `GetPageAsync`, `TryDisableAsync`) in `.../Infrastructure/Persistence/Repositories/VoucherRepository.cs`

### US1 - checkout with codes

- [X] T014 [US1] `VoucherPricing.Apply` in `.../Application/Vouchers/VoucherPricing.cs`
- [X] T015 [US1] `OrderTotals.Compute(lineDiscounts, deliveryDiscount)` in `.../Application/Orders/Common/OrderTotals.cs`
- [X] T016 [US1] `CheckoutPricing.PriceAsync(..., voucherCodes)` in `.../Application/Orders/Common/CheckoutPricing.cs`
- [X] T017 [US1] `voucherCodes` on `GetCheckoutQuoteQuery`, `SubmitOrderCommand`, their validators (`MustBeVoucherCodes`) and `OrdersController`; `Vouchers` and `Discount` on the responses and `OrderMapping.ToVouchers`
- [X] T018 [US1] `SubmitOrderCommandHandler`: freeze line discounts and redemptions, audit them, and `ClaimAndSaveAsync` instead of `SaveChangesAsync`
- [X] T019 [US1] `ReleaseForOrderAsync` inside the stage of `FailOrderCommandHandler` and of `CancelStep` in `CancelOrderCommands.cs`

### US2 - creating vouchers

- [X] T020 [US2] `CreateVoucherCommand`, `GetMyVouchersQuery`, `DisableVoucherCommand`, validators and `VoucherHandlers` in `.../Application/Vouchers/VoucherFeatures.cs`
- [X] T021 [US2] `server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/VouchersController.cs`; `voucher-route` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`

### US3 - money downstream

- [X] T022 [US3] Part `GoodsTotal` = `Σ(TotalPrice - ShopDiscount)` in `SubmitOrderCommandHandler`
- [X] T023 [US3] `ReturnParcelLine` with its discount in `.../Application/Returns/ReturnFeatures.cs` and `ReturnRepository.cs`
- [X] T024 [US3] `SellerLinesIn` revenue `Quantity × UnitPrice - ShopDiscount` in `.../Infrastructure/Persistence/Repositories/OrderInsights.cs`

### Tests and polish

- [X] T025 [P] `VoucherCheckoutTests.cs` (14) and `VoucherManagementTests.cs` (5); `SellerInsightsTests.cs` adjusted
- [X] T026 `OrderTestFixture` configures `EnableRetryOnFailure` and registers `IVoucherRepository`; `NotificationTests.Settling_inside_a_consumer_transaction_joins_it` opens its transaction inside the strategy (research D7)
- [X] T027 Both hand-opened voucher transactions moved inside `CreateExecutionStrategy().ExecuteAsync` after Bruno found a 500 in the container
- [X] T028 Direct repository tests for the two mutations that survived the first round (per-customer limit, repeated release)
- [X] T029 [P] Bruno: `bruno/order/` (create, quote, unknown code, checkout with the code) and `bruno/seller/` (seven voucher requests); the racy `bruno/auth/changing my password with a wrong current one is 400.yml` fixed
- [X] T030 Mutation checks - 12 of 12 caught (table in the PR)
- [X] T031 [P] Docs: new `docs/features/vouchers.md`; checkout, marketplace (rule 18), returns and seller-insights pages; reference regenerated (130 endpoints, 45 tables, the gateway route); decision 52; timeline; backlog (#108 half done); counts (server 726, Bruno 229); CLAUDE.md
- [X] T032 Merged as #153 on 2026-09-26 (`8b7003d`), Refs #108; the issue stayed open for part 2

## Dependencies

T009-T013 before everything; T014-T015 (pure) before T016-T019; US2 (T020-T021) independent of US1 except for the
repository; US3 (T022-T024) after T018. Tests first in each group.

## Implementation notes

- The plan named `IOrderRepository.SaveWithVouchersAsync`; the code is `IVoucherRepository.ClaimAndSaveAsync` (see the
  correction in [plan.md](plan.md)).
