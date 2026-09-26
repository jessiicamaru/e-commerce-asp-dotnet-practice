# Implementation Plan: Vouchers (part 1 - the server)

> Completed on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Branch**: `069-vouchers` | **Spec**: [spec.md](spec.md) | **Issue**: #108 | **PR**: #153 (merged 2026-09-26)

## Summary

Vouchers live in the Order service, which already prices checkouts. A voucher is composed of conditions, targets and
amounts per currency; a pure function, `VoucherPricing.Apply`, works out what each code takes off, and
`CheckoutPricing` calls it for both the quote and the order. Uses are claimed by guarded statements inside the order's
own transaction and given back by a failed or cancelled order. A seller pays for their own voucher out of their part's
goods total; the shop pays for the platform's and for free delivery. One migration, expand-only apart from one relaxed
CHECK.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR, FluentValidation, EF Core with Npgsql (`EnableRetryOnFailure`),
`Ecommerce.Shared` (`CurrencyOptions`, `IAuditTrail`, `ICurrentUser`), YARP

**Storage**: PostgreSQL `ecommerce_order_db` (5434) - six new tables, two new columns on `order_items`, one CHECK
replaced and one added

**Testing**: xUnit - pure pricing tests, and integration tests against real PostgreSQL including races; ApiGateway
tests; Bruno through the storefront's nginx

**Target Platform**: Order service behind the gateway (new `voucher-route`)

**Constraints**: the quote and the order agree to the unit; money per currency, never converted; claims commit with
the order or not at all; every hand-opened transaction runs inside the execution strategy

**Scale/Scope**: at most 5 codes per checkout, at most 100 targets per voucher

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

> **Corrections from the code (backfill)**: the column is named **`Benefit`**, not `BenefitType`, in both `vouchers`
> and `voucher_redemptions`; the target column is **`Type`**, not `TargetType`; `voucher_redemptions` also has
> **`Name`**. And the migration is not purely additive: it drops `CK_orders_no_discount_yet` and adds
> `CK_orders_discount_not_negative` and `CK_order_items_discounts` - a relaxation an earlier image still satisfies
> (research D8). Exact types are in [data-model.md](data-model.md).

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

> **Correction from the code (backfill)**: the method is **`IVoucherRepository.ClaimAndSaveAsync(applied,
> customerId)`**, not `IOrderRepository.SaveWithVouchersAsync`. It runs inside `CreateExecutionStrategy().ExecuteAsync`
> (research D7), and the per-customer guard is written `WHERE voucher_customer_uses."Uses" < @limit` with an absent
> limit passed as `int.MaxValue` rather than `@limit IS NULL OR ...` - the same effect. With no voucher it is just the
> one `SaveChangesAsync`. The 409 reads "Voucher X was just used up. Try again without it."

## Releasing

- `IVoucherRepository.ReleaseForOrderAsync(orderId)` runs inside the `stage` of `TrySettleAsync` (failed)
  and of `TryCancelAsync`:
  - `UPDATE voucher_redemptions SET "ReleasedAt" = now WHERE "OrderId" = @id AND "ReleasedAt" IS NULL
    RETURNING`;
  - then it decrements each voucher's counter and the customer's counter, never below 0.
- A repeat finds nothing to release.

(In the code the three statements are one: a CTE `released` → `totals` → the customer update, so exactly the
redemptions released give back a use.)

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

D7 and D8, found while building, and the alternatives for all eight: [research.md](research.md).

## Constitution check

- **III (atomic writes, idempotent messaging):** the claims and the order commit together, and the release is
  guarded.
- **IV (identity from the token):** the customer and the seller come from the token.
- **V (evidence):** tests against the real PostgreSQL, mutation checks, Bruno.

Against all five principles of [constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Vouchers are Order's facts, decided from Order's own tables (its orders answer "has bought before", its lines carry the seller id frozen from Catalog). No other service is asked anything new; no contract in `Ecommerce.Contracts` changed - the saga charges `TotalAmount` as before |
| **II. Clean Architecture Layering** | **Pass.** `VoucherPricing` is pure Application code with no dependencies; `IVoucherRepository` is declared in Application and implemented in Infrastructure; `VouchersController` only sends through MediatR. As with returns, the three management use cases share one file (`VoucherFeatures.cs`) rather than a folder each |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The claims, the order, its redemptions, the outbox message and the audit entry commit in one transaction or not at all; the claims are guarded statements (`UsedCount < TotalLimit`, `Uses < limit`) that affect zero rows past the limit; the release is one statement guarded on `ReleasedAt IS NULL`, run inside the settle or cancel transaction, so a repeat moves nothing; the counters carry CHECKs (`UsedCount <= TotalLimit`, `Uses >= 0`) |
| **IV. Identity Comes From the Token** | **Pass.** Whose voucher it is comes from the token's role and id - there is no seller id in the body; the customer of a checkout is `ICurrentUser`; another seller's voucher is 404, not 403 |
| **V. Evidence Over Assumption** | **Pass - and the principle earned its keep here.** 40 new tests against real PostgreSQL including the races; 12 of 12 mutations caught after two survived the first round and direct repository tests were added; Bruno in the container found that a hand-opened transaction outside the execution strategy answered 500 while all 257 tests passed, so the fixture now retries as production does (research D7) |

**Post-design re-check**: no violations. The two findings (D7, D8) changed the code, not the principles.

## Project Structure

### Documentation (this feature)

```text
specs/069-vouchers/
├── spec.md, plan.md, research.md (D1-D8), data-model.md, quickstart.md, tasks.md
├── contracts/http-api.md
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/{Voucher.cs, Order.cs, OrderItem.cs}, Enums/VoucherEnums.cs
├── Ecommerce.Order.Application/Vouchers/{VoucherPricing.cs, VoucherFeatures.cs, IVoucherRepository.cs}
├── Ecommerce.Order.Application/Orders/Common/{CheckoutPricing.cs, OrderTotals.cs, OrderMapping.cs, OrderResponses.cs}
├── Ecommerce.Order.Application/Orders/Commands/{SubmitOrder/*, FailOrder/FailOrderCommandHandler.cs, CancelOrder/CancelOrderCommands.cs}
├── Ecommerce.Order.Application/Orders/Queries/GetCheckoutQuote/GetCheckoutQuoteQuery.cs
├── Ecommerce.Order.Application/Returns/ReturnFeatures.cs                      # refund after discount
├── Ecommerce.Order.Infrastructure/Persistence/Configurations/{VoucherConfiguration.cs, OrderConfiguration.cs, OrderItemConfiguration.cs}
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/{VoucherRepository.cs, OrderRepository.cs, ReturnRepository.cs, OrderInsights.cs}
├── Ecommerce.Order.Infrastructure/Migrations/20260926073015_AddVouchers.cs
└── Ecommerce.Order.WebApi/Controllers/{VouchersController.cs, OrdersController.cs}
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json                     # voucher-route → order-cluster
server/tests/Ecommerce.Order.Tests/{VoucherPricingTests.cs (21), VoucherCheckoutTests.cs (14), VoucherManagementTests.cs (5), OrderTestFixture.cs, NotificationTests.cs}
bruno/order/ (3 new + checkout), bruno/seller/ (7 new), bruno/auth/ (a racy check fixed)
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- No screens (part 2, [specs/070](../070-voucher-screens/)).
- No category targets (D4), no editing, no public list of usable vouchers, no shop-funded free delivery.
- A returned parcel does not give its voucher use back.
