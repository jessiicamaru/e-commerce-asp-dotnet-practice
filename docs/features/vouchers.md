# Vouchers

Since specs/069 (#108, part 1), a customer can type voucher codes at checkout. The server works out what each
one takes off, spreads the discount over the lines it applies to, charges tax on what is left, and freezes all
of it on the order. There are two kinds:
- **Platform vouchers**, made by an administrator. They apply to anything, and the shop pays for them.
- **Shop vouchers**, made by a seller. They apply to that seller's lines only, and **the seller pays for them**
  out of their payout.

The server came in #153 (specs/069) and the screens in #154 (specs/070).

## Where it happens in the storefront

| Who | Page | What |
| :-- | :-- | :-- |
| **Customer** | `/checkout`, in the summary | A voucher box. **Apply** tries the code with the server first, by asking for the quote with it, and keeps the code only if the server takes it. A refusal appears in the server's words beside the box, and the summary stays as it was (research D1). Applied codes appear as chips and can be removed. The order is placed with exactly those codes. |
| **Customer** | `/orders/:id` and the checkout summary | Each voucher by its code, with the shop's name for a shop voucher and its amount. Each line's discount appears under its price. |
| **Seller** | `/shop/vouchers` (menu: Vouchers) | Their vouchers in words, for example "10% off · up to ₫100,000 · on orders from ₫500,000", with the uses, the dates and the status. **New voucher**: percent or amount off, the amounts per currency, dates and limits, first order in the shop, a minimum quantity, and their own products. **Disable** asks for confirmation first. |
| **Administrator** | `/admin/vouchers` (menu: Vouchers, Admin only) | The same page for the platform. It adds **free delivery** and **new customers**, and products are picked from the whole catalogue. |

The form offers a role only what the server accepts from it, and the server refuses the rest in its own words
(specs/070 research D2).

## What people can do

| Who | Can |
| :-- | :-- |
| **Customer** | Send up to 5 codes with the checkout quote (`?voucherCodes=`) and the order (`voucherCodes: []`). They see each voucher applied, its amount, and each line's discount. |
| **Seller** | Create vouchers for their own shop, list theirs, and disable one. They can never give free delivery. |
| **Administrator** | Create platform vouchers, list them, and disable any voucher, a seller's included. |

## How a voucher is stored

One voucher, made of composable parts. A new case is a new row, not a new column.

| Table | Holds |
| :-- | :-- |
| `vouchers` | The code (unique, upper-case) and the owner (`SellerId`, null = platform). The benefit: `Percent`, `FixedAmount` or `FreeShipping`. The dates, the status, `TotalLimit` / `UsedCount` and `PerCustomerLimit`. |
| `voucher_conditions` | `NewCustomer`, `FirstOrderInShop` and `MinQuantity` (with its value). Every condition must hold. |
| `voucher_targets` | `Product` or `Variant` ids. No targets means every line the voucher may touch. |
| `voucher_amounts` | Per currency: `FixedValue`, `MaxDiscount` (the cap) and `MinSubtotal` (the minimum spend). |
| `voucher_customer_uses` | How many orders each customer holds the voucher on. This is the counter the per-customer limit guards. |
| `voucher_redemptions` | The voucher used on an order, **frozen**: code, name, owner, benefit, amount and currency. `ReleasedAt` is set when the use is given back. |
| `order_items` | `ShopDiscount` and `PlatformDiscount`, frozen on each line. |

How the examples that shaped it are stored:
- **30% off orders over 1,000,000₫, capped at 200,000₫**: `Percent 30`, and an amount row for VND with
  `MinSubtotal` 1,000,000 and `MaxDiscount` 200,000.
- **20% off one item**: `Percent 20`, with a `Variant` target.
- **New customers**: a `NewCustomer` condition and `PerCustomerLimit` 1.
- **Free delivery over 300,000₫**: `FreeShipping`, with `MinSubtotal` 300,000.

## Rules and guarantees

1. **Only the server computes.** The request names codes and nothing else.
   - `VoucherPricing.Apply` is one pure function, called by `CheckoutPricing` for both the quote and the order,
     so the two agree to the unit.
2. **Order of application and stacking.**
   - Each shop's voucher applies to that shop's lines first. The platform's goods voucher then applies to what
     is left. Free delivery comes last.
   - At most one voucher per shop, one platform goods voucher, and one free-delivery voucher.
3. **Money per currency, never converted** (specs/022).
   - A voucher is usable only in a currency it has an amount row for. This holds even for a percentage, because
     its cap and minimum are amounts (research D3).
   - Every amount must fit its currency: no decimals in dong.
4. **Rounding.**
   - A voucher's discount is spread over its lines in proportion to what is left of each, in whole minor units.
   - The remainder goes to the largest line, so the shares add up exactly.
   - A percentage rounds half away from zero, like tax.
5. **Tax is on what the customer pays** (research D2): each line after its discounts, and the delivery after
   free delivery.
   - The discount part (specs/012) is the sum of every voucher.
   - `CK_orders_no_discount_yet` became `CK_orders_discount_not_negative`. The parts still have to add up to the
     total, and `CK_order_items_discounts` keeps each line's discounts between 0 and its price.
6. **Who pays** (research D1).
   - A seller's part freezes `GoodsTotal` as their goods less **their own** voucher, so their commission and
     payout follow.
   - A platform voucher leaves the seller's terms untouched, so the shop's margin carries it.
   - Free delivery is platform-only, so the seller's delivery share stays whole.
7. **Uses are claimed with the order, in one transaction** (research D5).
   - A guarded `UPDATE vouchers ... WHERE "UsedCount" < "TotalLimit"` claims a use.
   - A guarded upsert on `voucher_customer_uses` counts the customer's.
   - Then the one save, in the same transaction.
   - Of many checkouts racing for the last use, one order is placed and the others get a 409. Nothing is saved
     for them.
8. **A failed or cancelled order gives its uses back, once.** One statement marks the redemptions released and
   takes one off each counter. It runs inside the settlement's or the cancellation's own transaction. A
   returned parcel does not give the use back.
9. **Refusals are 409s in words**: expired, not started, used up, already used by you, below the minimum,
   applies to nothing in your cart, first order only, not in this currency. An unknown code and a disabled one
   read the same ("cannot be used"), so guessing learns nothing (D6).
10. **Downstream:**
    - a returned parcel refunds what was paid for it, goods less discount plus their tax;
    - a seller's revenue in [their insights](seller-insights.md) is their lines less their own voucher.

## API

| Method | Path | Who |
| :-- | :-- | :-- |
| `POST` | `/api/vouchers` | Admin (platform voucher) or Seller (shop voucher). Whose it is comes from the token. |
| `GET` | `/api/vouchers/mine` | The caller's own: the platform's for Admin, theirs for a seller. |
| `POST` | `/api/vouchers/{id}/disable` | The owner, or Admin for any voucher. Not yours is a 404; already disabled is a 409. |
| `GET` | `/api/orders/quote?...&voucherCodes=A&voucherCodes=B` | Customer |
| `POST` | `/api/orders`, body `voucherCodes: [...]` | Customer |

The gateway has a new route, `/api/vouchers/{**catch-all}`, to Order. The audit log records `VoucherCreated`
and `VoucherDisabled` (category Order).

## Tests

| Where | Proves |
| :-- | :-- |
| `Ecommerce.Order.Tests/VoucherPricingTests` | Every rule of the pure function: the user's examples, targets, shop before platform, stacking, the currency rule, allocation and its remainder, rounding, dates, limits, conditions, and tax after discount. |
| `Ecommerce.Order.Tests/VoucherCheckoutTests` | The quote and the order agree, and the order freezes the discounts. Free delivery. The seller's terms. A refusal places nothing. The race for the last use. The per-customer limit, both through checkout and at the claim itself. Release on failure and on cancellation, once. New customers. The refund. The seller's revenue. |
| `Ecommerce.Order.Tests/VoucherManagementTests` | Whose a voucher is. Codes are upper-case and unique. What a voucher may say. Each owner sees only theirs. Disabling is by the owner, or an administrator for any. |
| Bruno `order/` 5-8 | An administrator makes a voucher. The quote takes it off. An unknown code is a 409. **The checkout claims it through the gateway.** |
| Vitest `pages/checkout`, `pages/order`, `pages/shop-vouchers`, `components/voucher`, `utils/voucher`, `services/voucher` | Checkout: a code is tried before it is kept, a refusal is shown beside the box, and the order is placed with the codes. The totals name each voucher. What the form sends for each role. How a voucher is worded. Disable asks for confirmation first. The URLs, and codes repeated in the quote. |
| Bruno `seller/` 60-66 | A seller's voucher. The seller's list. No free delivery for a seller. Disable, and again (409). A customer is refused (403), and so is a request without a token (401). |

⚠️ **The Order test fixture now uses the retries production configures** (`EnableRetryOnFailure`). Without them,
a transaction opened by hand outside `CreateExecutionStrategy()` passed every test and answered 500 in every
container. Both voucher transactions did exactly that until Bruno disabled a voucher.

## Known limits

- **Variant targets only through the API.** The screens pick products (specs/070 research D3).
- **No category targets** (research D4). Order's lines do not know their category, and Catalog's pricing answer
  would have to carry it.
- **A voucher cannot be edited.** Disable it and create another. Orders keep what they used.
- **No public list** of the vouchers a shopper could use.
- **The admin Overview's top products are before discount.** Its revenue, the orders' totals, is after.

## History

| Spec | PR | Added |
| :-- | :-- | :-- |
| [069-vouchers](../../specs/069-vouchers/) | #153 | Vouchers on the server: the model, pricing, claims and releases, and the management API (#108, part 1). |
| [070-voucher-screens](../../specs/070-voucher-screens/) | #154 | The checkout's voucher box, vouchers on the order, and the voucher pages for sellers and administrators (#108, part 2). |
