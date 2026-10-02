# Contracts: Checkout says how payment works and how long delivery takes

## HTTP (Order, through the gateway)

- `PUT /api/orders/delivery/options/{code}` body gains `minDays` and `maxDays` (`int?`, both or neither, 0..60,
  min <= max - else 400 naming `MinDays`/`MaxDays`).
- `GET /api/orders/delivery` - each option gains `minDays`, `maxDays`.
- `GET /api/orders/shipping-options` and `GET /api/orders/quote` (`shippingOption`) gain `minDays`, `maxDays`; `null`
  when not set. An order's `shippingOption` reports `null` for both (not frozen).

## HTTP (Payment) - unchanged, newly read by the storefront

`GET /api/payment/health` -> `{ "provider": "Stub - no money is moved", ... }`.

## Configuration

`Shipping:Options:N:MinDays`, `Shipping:Options:N:MaxDays` - optional, both or neither, same rules; refused at startup
otherwise.

No message or gRPC change.
