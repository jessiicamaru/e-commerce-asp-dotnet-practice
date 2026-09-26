# Tasks: Vouchers (part 1 - the server)

- [ ] T001 `VoucherPricing` (pure) with unit tests in `server/tests/Ecommerce.Order.Tests/VoucherPricingTests.cs`. Cover:
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
- [ ] T002 `OrderTotals.Compute` with discounts: tax on the discounted amounts, and the CHECK identity. Tests in `OrderTotalsTests`.
- [ ] T003 The entities, configurations and migration `AddVouchers`, the `order_items` discount columns, and `IVoucherRepository`.
- [ ] T004 `CheckoutPricing`, the quote and submit with codes: the lines' discounts frozen, the earnings less the shop discount, the claims in the order's transaction, and the redemptions. Integration tests in `VoucherCheckoutTests`, including the race for the last use and the per-customer limit.
- [ ] T005 Release on a failed or cancelled order, idempotent.
- [ ] T006 The refund of a returned parcel after discount, and seller insights after the shop discount. Tests.
- [ ] T007 `VouchersController`: create, mine, disable. Validators, audit, the gateway route. Tests in `VoucherManagementTests`.
- [ ] T008 Bruno, the reference, the docs, and the mutation checks.
