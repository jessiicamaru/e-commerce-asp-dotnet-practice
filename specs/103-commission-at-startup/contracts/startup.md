# Contract: Order's startup

No HTTP route, message or gRPC call changes. This feature has no external contract to describe beyond what the
service does when it starts.

## Before

Order started without `Marketplace:CommissionRate`, reported `/health` as healthy, and answered the first
`POST /api/orders` and `GET /api/orders/quote` with a 500.

## After

Order started without the setting, or with a rate outside [0, 1), exits before listening. It logs the
`InvalidOperationException` from `ConfiguredCommissionRate`:

```text
Marketplace:CommissionRate is not configured. Checkout cannot record what a seller is owed.
Marketplace:CommissionRate is 1.5; it must be at least 0 and below 1.
```

With a valid rate nothing changes.
