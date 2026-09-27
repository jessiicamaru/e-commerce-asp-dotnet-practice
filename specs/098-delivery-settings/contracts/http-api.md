# HTTP Contract: Administrators manage delivery and the carrier

**Feature**: [spec.md](../spec.md). All through the gateway's `/api/orders/**` route.

## `GET /api/orders/delivery` - Admin

```json
{
  "options": [ { "code": "standard", "name": "Standard delivery", "isActive": true, "sortOrder": 0, "prices": { "USD": 2, "VND": 30000 } } ],
  "carrier": { "name": "Shop delivery", "trackingUrlTemplate": null }
}
```

## `PUT /api/orders/delivery/options/{code}` - Admin

```json
{ "name": "Express delivery", "isActive": true, "sortOrder": 1, "prices": { "VND": 60000, "USD": 4 } }
```

Creates the option when the code is new, otherwise updates it (the code never changes). **200** `DeliveryOptionResponse`.

| Status | When |
| :-- | :-- |
| 400 | code not `^[a-z0-9-]{1,32}$`; empty name or > 100; a currency the shop does not sell in; a negative price; an amount the currency cannot hold; offered without a price in the default currency |
| 409 | turning off the last option on offer |

## `PUT /api/orders/delivery/carrier` - Admin

```json
{ "name": "GHN", "trackingUrlTemplate": "https://ghn.example/track/{reference}" }
```

**200** `CarrierResponse`. **400**: empty name; a template without `{reference}`, not absolute, or not http(s). An empty
or null template means references are shown as text.

## `GET /api/orders/delivery/carrier` - anyone

**200** `{ "name": "...", "trackingUrlTemplate": "..." | null }`.

## Changed in behaviour, not shape

- `GET /api/orders/shipping-options`, `GET /api/orders/quote`, `POST /api/orders`: read the table - an option turned off is
  not offered and is refused like an unknown code.

## Bruno

`order/` seq 14-18: staff read the settings (200), a customer (403), the carrier given a tracking page (200), the carrier is
public (200), the carrier put back.
