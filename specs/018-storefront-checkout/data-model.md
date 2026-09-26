# Data Model: Checkout and Order History

> Written on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md)

**No table changed and no migration was added.** The pull request says so: "additive: no contract, no
migration". The order's stored parts are specs/012's columns, unchanged.

## The quote (computed, never stored)

`CheckoutQuoteResponse` in `Orders/Queries/GetCheckoutQuote/GetCheckoutQuoteQuery.cs`:

| Field | Type | From |
| :--- | :--- | :--- |
| `Items` | `List<OrderItemResponse>` (product id, name, quantity, unit price, line total, line tax) | the cart, priced by Catalog |
| `ShippingAddress` | `ShippingAddressResponse` | Identity, for the caller's token |
| `ShippingOption` | `{ Code, Name }` | Order's `Shipping:Options` |
| `ShippingPrice`, `Subtotal`, `TaxTotal`, `DiscountTotal`, `TaxRate`, `TotalAmount` | `decimal` | `OrderTotals.Compute` (specs/012) |

Unlike the order's own response, none of these is nullable: a quote always has every part.

`CheckoutPricing.PriceAsync` returns a `PricedCheckout` (lines, address, shipping option, tax rate,
totals) that both handlers read; the order handler turns it into rows, the quote handler into this
response.

## Client types (`client/src/api/orders.ts`)

| Type | Fields |
| :--- | :--- |
| `Totals` | `subtotal`, `shippingPrice`, `taxTotal`, `discountTotal`, `taxRate` (all nullable, for orders from before specs/012), `totalAmount` |
| `Quote` | `Totals` + `items`, `shippingAddress`, `shippingOption` |
| `Order` | `Totals` + `orderId`, `status`, `failureReason`, `createdAt`, `updatedAt`, `items`, `shippingAddress`, `shippingOption`, `trackingReference` |
| `OrderSummary` | `orderId`, `totalAmount`, `status`, `failureReason`, `itemCount`, `createdAt`, `updatedAt` |
| `OrderPage` | `items`, `page`, `pageSize`, `totalCount` |
| `Choice` | `addressId \| null`, `shippingOption` |

Nothing is stored in the browser; the order page passes `justPlaced` in router state to word its
heading.
