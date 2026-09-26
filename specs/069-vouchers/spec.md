# Feature Specification: Vouchers (part 1 - the server)

**Feature Branch**: `069-vouchers` | **Created**: 2026-09-26 | **Issue**: #108 (part 1; part 2 is the screens)

## Why

An order's total has had a discount part since specs/012, and it has always been zero. There is no way for
the shop or a seller to lower a price for a campaign, reward a first order, or give free delivery.

The user agreed the design on 2026-09-26:
- shop vouchers and platform vouchers;
- one voucher stored as composable parts (conditions, targets, amounts per currency, limits);
- only the server computes the discount.

## User Scenarios

### US1 - A customer checks out with codes (P1)

The quote and the order take up to 5 voucher codes. The server checks each code and computes the discount,
split across the lines it applies to. It then charges tax on the discounted prices and shows every applied
voucher with its amount.

**Acceptance**:
1. "Orders over 1,000,000₫ get 30% off, capped at 200,000₫" gives exactly that on an eligible order. On
   999,999₫ it is refused with a 409 that names the minimum.
2. "20% off one variant" discounts only that variant's line.
3. "New customers" is refused to anybody who has bought before.
4. Free delivery takes the delivery charge, and its tax, off.
5. The quote and the order agree to the unit. The discount part is non-zero. The CHECK on the order's parts
   holds.
6. A code that is used up, expired, disabled, not yet started, unknown, or not offered in the order's
   currency is refused with a 409 in words.
7. Two customers taking the last use at once: exactly one order is placed, and the other gets a 409.
8. A failed or cancelled order gives its uses back, once.

### US2 - The shop and sellers create vouchers (P1)

- An **administrator** creates platform vouchers. They apply to every line, from any seller, and the shop
  funds them.
- A **seller** creates shop vouchers. They apply to that seller's lines only, and **the seller funds them**:
  the discount comes out of the seller's payout.
- Both can list their own vouchers and disable one. Somebody else's voucher is a 404.

### US3 - Money stays right downstream (P1)

- A seller's `GoodsTotal`, and so their commission and payout, is their goods **less their own shop
  voucher**. A platform voucher does not reduce it.
- Returning a parcel refunds what was actually paid for it: goods less discount, plus tax.
- A seller's insights count their revenue after their own discount.

## Rules

- **Stacking:** at most one platform voucher on the goods, one free-delivery voucher, and one voucher per
  shop. Shop vouchers are taken off first, then the platform voucher applies to what remains.
- **Money per currency, never converted** (specs/022). A voucher is usable only in a currency it has an
  amount row for, even a percentage one, because the cap and the minimum are amounts.
- **Rounding:** every amount is rounded to the currency's minor unit, half away from zero. A voucher's
  discount is spread over its lines in proportion to their price, and the remainder goes to the largest line.
- **A percentage** is 1 to 100 and is capped by `MaxDiscount` when one is set. **A fixed amount** is never
  more than the lines it applies to.
- **Conditions, all of which must hold:**
  - the minimum spend (`MinSubtotal`, per currency, measured over the lines the voucher applies to);
  - `NewCustomer`: no sold order before;
  - `FirstOrderInShop`: no sold order from that shop before (shop vouchers only);
  - `MinQuantity`: units on the lines the voucher applies to.
- **Targets:** none means everything the voucher may touch. Otherwise the voucher applies to `Product` or
  `Variant` ids. `Category` is **not** in part 1 (research D4).
- **Limits:**
  - a start and an optional end;
  - `TotalLimit` and `PerCustomerLimit`, each claimed in a guarded statement inside the order's own
    transaction.

  A failed or cancelled order releases its uses. A returned parcel does not.
- **Frozen on the order:** each line's `ShopDiscount` and `PlatformDiscount`, and a redemption row per
  voucher with its code and amount. Editing or disabling a voucher later changes no order.

## Out of scope (part 1)

- The storefront screens (part 2).
- Category targets.
- Editing a voucher. Disable it and create a new one.
- A public list of the vouchers a shopper could use.
- Shop-funded free delivery.
