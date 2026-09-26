# HTTP Contract: Vouchers (part 1 - the server)

> Written on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](../spec.md)

All on the Order service through the gateway on `:5000`. `/api/vouchers/**` is a **new gateway route**
(`voucher-route` → `order-cluster`); `/api/orders/**` was already routed. **No message contract changed**: nothing in
`Ecommerce.Contracts` gained a field, and the saga charges `TotalAmount` as before. Request and response records gained
optional fields only.

Errors: 400 ProblemDetails with `errors` for validation; 404 `Voucher not found.`; 409 with the reason in `detail`; 401
without a token; 403 without the route's role.

---

## `POST /api/vouchers` - `Admin` or `Seller`

An administrator's voucher is the platform's; a seller's is their shop's. Whose it is comes from the token - there is
no seller id in the body.

```json
{
  "code": "BRUNO-1727340000000",
  "name": "Bruno ten percent",
  "benefit": "Percent",
  "percent": 10,
  "startsAt": null,
  "endsAt": null,
  "totalLimit": null,
  "perCustomerLimit": null,
  "amounts":    [ { "currency": "VND", "fixedValue": null, "maxDiscount": 50000, "minSubtotal": null } ],
  "conditions": [ { "type": "MinQuantity", "value": 2 } ],
  "targets":    [ { "type": "Variant", "id": "…" } ]
}
```

| Status | When |
| :-- | :-- |
| 200 | Created; answers the `VoucherSummary` below |
| 400 | Code not 3-32 letters/digits/dashes starting with a letter or digit; no name; unknown benefit; a percentage outside 1-100 or with more than two decimals; a percentage on a non-percent voucher; `FreeShipping` by a seller; `EndsAt` not after `StartsAt`; limits below 1 or a per-customer limit above the total; no amounts; a currency twice or unsupported; an amount the currency cannot hold; `FixedAmount` without a `FixedValue` > 0 in every currency, or a `FixedValue` on another benefit; a condition twice, unknown, `MinQuantity` without a value 1-1000, a value on another condition, `FirstOrderInShop` on a platform voucher; more than 100 targets, an unknown target type, an empty id, targets on a free-delivery voucher |
| 409 | "The code X is taken." - in any case; also when two creators race, decided by the unique index |

## `GET /api/vouchers/mine?page=1&pageSize=12` - `Admin` or `Seller`

The platform's vouchers for an administrator, the caller's own for a seller; newest first. `pageSize` 1-50.

```json
{
  "items": [ {
    "id": "…", "code": "BRUNO-…", "name": "Bruno ten percent", "isPlatform": true,
    "benefit": "Percent", "percent": 10, "status": "Active",
    "startsAt": "…", "endsAt": null, "totalLimit": null, "usedCount": 1, "perCustomerLimit": null,
    "amounts": [ { "currency": "VND", "fixedValue": null, "maxDiscount": 50000, "minSubtotal": null } ],
    "conditions": [], "targets": [], "createdAt": "…"
  } ],
  "page": 1, "pageSize": 12, "totalCount": 1
}
```

## `POST /api/vouchers/{id}/disable` - `Admin` or `Seller`

`Active` → `Disabled`, one guarded statement with its audit entry. Answers the `VoucherSummary`.

| Status | When |
| :-- | :-- |
| 200 | Disabled |
| 404 | No such voucher, or a seller disabling one that is not theirs (not 403: a 403 would confirm the id is real) |
| 409 | "This voucher is already disabled." |

---

## Checkout - existing endpoints, new optional input and output

### `GET /api/orders/quote?addressId=&shippingOption=&voucherCodes=A&voucherCodes=B`
### `POST /api/orders` `{ "addressId": "…", "shippingOption": "…", "voucherCodes": ["A", "B"] }`

`voucherCodes` optional; at most 5, each at most 32 characters (400 otherwise). Codes match whatever their case.

New in both responses:

```json
{
  "items": [ { "…": "…", "discount": 50000 } ],
  "discountTotal": 50000,
  "vouchers": [ { "code": "BRUNO-…", "name": "Bruno ten percent", "isShop": false, "sellerName": null,
                  "benefit": "Percent", "amount": 50000 } ]
}
```

`discount` on a line is its `ShopDiscount + PlatformDiscount`; `vouchers` lists every applied voucher with what it took
off in the order's currency. `GET /api/orders/{id}` and the order detail read the same frozen values.

| Status | Refusal (409 `detail`) |
| :-- | :-- |
| 409 | "Voucher X cannot be used." - unknown **or** disabled (same words) |
| 409 | "Voucher X starts on yyyy-MM-dd." / "Voucher X has expired." / "Voucher X has been used up." |
| 409 | "You have already used voucher X as many times as it allows." |
| 409 | "Voucher X cannot be used in USD." (no amount row) |
| 409 | "Voucher X does not apply to anything in your cart." |
| 409 | "Voucher X needs an order of at least 1000000 VND on what it applies to." |
| 409 | "Voucher X is for a first order only." / "… for a first order from this shop only." / "… needs at least N items it applies to." |
| 409 | "Only one voucher per shop / one shop-wide voucher / one free delivery voucher can be used on an order (…)." |
| 409 | "Voucher X: this delivery is already free." |
| 409 | `POST /api/orders` only: "Voucher X was just used up. Try again without it." - the guarded claim moved nothing; nothing was saved |

---

## Authorization

| Endpoint | Admin | Seller | Customer | Anonymous |
| :-- | :-- | :-- | :-- | :-- |
| `/api/vouchers/**` | platform's | own shop's | 403 | 401 |
| quote, `POST /api/orders` with codes | as any customer | as any customer | 200 | 401 |

## Audit

`VoucherCreated` (after: the summary) and `VoucherDisabled` (before `Active`, after `Disabled`), category `Order`,
subject `Voucher`; the order's own `OrderPlaced` entry now carries `DiscountTotal`, the vouchers and each line's
discounts.
