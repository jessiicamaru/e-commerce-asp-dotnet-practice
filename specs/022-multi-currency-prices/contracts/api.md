# Contracts: Two Price Lists

> Completed on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

The **Messages** and **gRPC** sections below are also written out, with publishers, consumers and
compatibility, in [messages.md](messages.md) and [grpc.md](grpc.md) (added 2026-09-27).

## How a request says which currency it wants

Every public read accepts, in order of precedence:

1. `?currency=USD` on the query string;
2. `X-Currency: USD`;
3. the configured default (`VND`).

An unsupported code falls back to the default. Responses carry `X-Currency` saying which currency
their amounts are in - so a client never has to assume it got what it asked for. They also carry
`Vary: X-Currency`, without which a cache would serve one shopper's dollar prices to the next shopper asking
in dong.

There is no standard request header for a currency and none is pretended (research D1).

## Catalog

| Endpoint | Change |
| :-- | :-- |
| `GET /api/products`, `GET /api/products/{id}` | prices come back in the requested currency; **null** where the variant has no price in it |
| `PUT /api/products/{id}/variants/{variantId}/prices/{currency}` — **Admin** | `{ amount }` → 200. Creates or replaces that currency's price. `400` for an unsupported currency, an amount of zero or less, or one the currency cannot hold; `404` for an unknown product or variant (added 2026-09-27) |
| `DELETE /api/products/{id}/variants/{variantId}/prices/{currency}` — **Admin** | 204; the variant stops being sold in that currency. Refused with 409 for the default currency, which lives on the variant itself |

`ProductResponse` and `VariantResponse` gain `currency` - which currency the amounts in this response
are in - and their `price` becomes **nullable**.

> A nullable price is a breaking change to the shape a client reads. It is taken deliberately rather
> than returning `0`: zero is a price, and a shop that shows a free camera is worse than one that
> shows a blank.

`VariantResponse.sellable` already exists and now also answers "priced in this currency".

## Order

`OrderDetailResponse`, `OrderResponse` and the quote gain `currency`: the currency the order was
placed in, and the currency of every amount on it.

`GET /api/orders/shipping-options` returns only the options priced in the request's currency, each
with the amount in it.

`POST /api/orders` takes no new field - the request's currency is used, exactly as its language is.

## Payment

No public endpoint. `payments.Currency` is reported by `/health`? **No** - it is a per-row fact, not a
service-wide one. It surfaces only where a payment is read, which today is nowhere public.

## Messages

```csharp
public record OrderSubmittedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    List<OrderItemDto> Items,
    DateTime CreatedAt,
    string Currency = "");      // additive; "" = the shop's default

public record ProcessPaymentCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency = "");      // additive; "" = the shop's default
```

⚠️ **The Orchestrator relays the first into the second.** It must be rebuilt with this change or it
will deserialise the event into its older record, drop the currency, and charge the right number in
the wrong money - the specs/020 `VariantId` defect, on the field where it cannot be detected
afterwards (research D4).

## gRPC

`catalog_pricing.proto`:

```proto
message PriceVariantsRequest {
  repeated string variant_ids = 1;
  string language = 2;
  // Which currency to price in (specs/022). Empty = the shop's default, which is what an Order built
  // before this feature sends. A variant with no price in it is answered as NOT SELLABLE - never
  // converted, never the default currency's amount relabelled.
  string currency = 3;
}

message DescribeVariantsRequest {
  repeated string variant_ids = 1;
  string language = 2;
  string currency = 3;
}

message PricedVariant {
  // ... unchanged fields ...
  // `price` stays a decimal-as-string, and is now EMPTY when the variant has no price in the
  // requested currency. An empty price always comes with sellable = false.
  string price = 6;
  bool sellable = 7;
  string currency = 8;   // which currency `price` is in, echoed so a caller need not assume
}
```

Additive: no method changes, no field is renumbered, and an older caller sending no currency gets the
default currency's prices, which is what it gets today.

## Storefront

- A currency switcher beside the language switcher, and **visibly not the same control**.
- The choice is stored per visitor and sent as `X-Currency`; changing it invalidates every cached
  query.
- Amounts are formatted with `Intl.NumberFormat(language, { style: 'currency', currency })` - the
  language decides the separators, the currency decides the symbol and the decimals.
- A variant with no price in the chosen currency shows "not sold in USD" in place of a price, and
  cannot be added to the cart.

## Refusals shown outside Development (added 2026-09-27)

`GlobalExceptionHandler` in `Ecommerce.Shared` now keeps the `detail` of the deliberately mapped exceptions
in every environment, so checkout's 409 names the variant and the currency ("Not sold in USD: ...") in a
deployed image. Unmapped exceptions still hide their message, because that text can carry internals.
Found by a Bruno test asserting on the wording of a 409 (tasks, "What building this found" 3).
