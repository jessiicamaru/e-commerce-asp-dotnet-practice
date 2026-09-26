# Implementation Plan: Vouchers (part 1 - the server)

**Branch**: `069-vouchers` | **Spec**: [spec.md](spec.md) | **Issue**: #108

## Data (Order's database, one migration `AddVouchers`)

| Table | Columns |
| :-- | :-- |
| `vouchers` | `Id`, `Code` (unique, stored upper-case), `SellerId` (null = platform), `Name`, `BenefitType` (`Percent` / `FixedAmount` / `FreeShipping`), `Percent`, `StartsAt`, `EndsAt`, `Status` (`Active` / `Disabled`), `TotalLimit`, `UsedCount`, `PerCustomerLimit`, `CreatedBy`, `CreatedAt`, `UpdatedAt` |
| `voucher_conditions` | `VoucherId`, `Type` (`NewCustomer` / `FirstOrderInShop` / `MinQuantity`), `Value` |
| `voucher_targets` | `VoucherId`, `TargetType` (`Product` / `Variant`), `TargetId` |
| `voucher_amounts` | `VoucherId`, `Currency`, `FixedValue`, `MaxDiscount`, `MinSubtotal` (PK voucher + currency) |
| `voucher_customer_uses` | `VoucherId`, `CustomerId`, `Uses` (PK voucher + customer) |
| `voucher_redemptions` | `Id`, `VoucherId`, `OrderId`, `CustomerId`, `Code`, `SellerId`, `BenefitType`, `Amount`, `Currency`, `CreatedAt`, `ReleasedAt`; unique `(VoucherId, OrderId)` |
| `order_items` | **+** `ShopDiscount` and `PlatformDiscount`, `decimal(18,2) NOT NULL DEFAULT 0` |

The migration only adds (expand only). An older image writes lines without the new columns, and they default
to 0.

## Pricing

- `Vouchers/VoucherPricing.Apply` is a pure function. It takes the lines, the delivery price, the currency,
  the vouchers and the customer's facts, and returns:
  - each line's shop and platform discount;
  - the delivery discount;
  - the applied vouchers with their amounts.

  It throws a `ConflictException` naming the code and the reason.
- `OrderTotals.Compute` takes each line's discount and the delivery discount:
  - the tax on a line is on its price after discount;
  - the delivery tax is on the delivery after discount;
  - `Discount` is the sum of all of them;
  - `Total = Subtotal + Delivery + Tax - Discount`, so the CHECK holds.
- `CheckoutPricing.PriceAsync(..., voucherCodes)` loads the vouchers through `IVoucherRepository` (with its
  conditions, targets and amounts). It asks whether the caller has bought before, overall and per shop, only
  when a condition needs it. It returns the applied vouchers in `PricedCheckout`.

## Placing the order

- The lines freeze their discounts. `Earnings.ForPart` gets `goods = Σ(gross - ShopDiscount)`.
- `IOrderRepository.SaveWithVouchersAsync(claims, ...)`, in one transaction:
  - for each voucher, `UPDATE vouchers SET "UsedCount" = "UsedCount" + 1 WHERE "Id" = @id AND "Status" =
    'Active' AND ("TotalLimit" IS NULL OR "UsedCount" < "TotalLimit")`;
  - then `INSERT INTO voucher_customer_uses ... ON CONFLICT DO UPDATE SET "Uses" = "Uses" + 1 WHERE @limit IS
    NULL OR "Uses" < @limit RETURNING`;
  - then the one `SaveChanges`: the order, the redemptions, the outbox message and the audit entry;
  - then commit.

  If any claim moves nothing, the transaction rolls back and the caller gets a 409 naming the code.

## Releasing

- `IVoucherRepository.ReleaseForOrderAsync(orderId)` runs inside the `stage` of `TrySettleAsync` (failed)
  and of `TryCancelAsync`:
  - `UPDATE voucher_redemptions SET "ReleasedAt" = now WHERE "OrderId" = @id AND "ReleasedAt" IS NULL
    RETURNING`;
  - then it decrements each voucher's counter and the customer's counter, never below 0.
- A repeat finds nothing to release.

## Downstream

- `ReturnParcelLine` gains its discount, and the refund is `Σ(gross - discount + tax)`.
- `SellerLinesIn` measures revenue as `Quantity × UnitPrice - ShopDiscount`.
- Responses:
  - `OrderItemResponse` and `OrderItemDetailResponse` gain `Discount`;
  - the order, the quote and the submit response gain `Vouchers[]` (code, shop or platform, seller name,
    amount).

## API (`VouchersController`, `api/vouchers`, a new gateway route to Order)

| Method | Path | Who |
| :-- | :-- | :-- |
| `POST` | `/api/vouchers` | Admin (makes a platform voucher) or Seller (makes a voucher for their shop) |
| `GET` | `/api/vouchers/mine?page=` | Admin sees the platform vouchers; a seller sees their own |
| `POST` | `/api/vouchers/{id}/disable` | the owner, or Admin for any. Not yours is a 404; already disabled is a 409 |
| `GET` | `/api/orders/quote?...&voucherCodes=A&voucherCodes=B` | the customer |
| `POST` | `/api/orders` body `voucherCodes: []` | the customer |

The audit trail records `VoucherCreated` and `VoucherDisabled` in the `Order` category.

## Research

- **D1 - who funds what.** A shop voucher comes out of the seller's goods, so their commission is taken on
  less and their payout drops. A platform voucher is the shop's cost. The seller's terms are unchanged, and
  the shop's margin carries it. Free delivery is platform-only in part 1: the seller's delivery share stays
  whole, and the shop pays the carrier.
- **D2 - tax after discount.** The customer pays tax on what they pay. A refund of a returned parcel is then
  exactly what was paid for it.
- **D3 - a voucher needs an amount row for the currency, even a percentage one.** The cap and the minimum
  are amounts, and a percentage with no cap in a currency nobody thought about is an open cheque.
- **D4 - no category targets yet.** Order's lines do not know their category. It would need Catalog's
  pricing answer to carry it, which is a proto change best made with the screen that needs it.
- **D5 - two counters, both guarded.** `UsedCount` on the voucher and `voucher_customer_uses` per customer.
  Counting redemptions instead would race: two concurrent checkouts both count 0.
- **D6 - an unknown code is a 409 with the same words as a disabled one** ("cannot be used"). This does not
  confirm which codes exist to somebody guessing.

## Constitution check

- **III (atomic writes, idempotent messaging):** the claims and the order commit together, and the release is
  guarded.
- **IV (identity from the token):** the customer and the seller come from the token.
- **V (evidence):** tests against the real PostgreSQL, mutation checks, Bruno.
