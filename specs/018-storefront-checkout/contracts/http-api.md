# HTTP Contract: Checkout and Order History

> Written on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](../spec.md) | **Decision**: [research.md D1, D2](../research.md)

One endpoint is new. The others are recorded because the pages depend on their exact behaviour. All on
Order (`:5059`), through the gateway at `http://localhost:5000/api/orders`, signed in unless marked.
Errors are RFC 7807 ProblemDetails. **No message contract changed** and no migration was added.

---

## `GET /api/orders/quote` - signed in - **new**

Query: `addressId` (optional; omitted means the default address), `shippingOption` (required).

`200` - what `POST /api/orders` would charge for the same choices, in the same parts, **placing,
staging and publishing nothing**. From the pull request's run (a GB address, express):

```json
{
  "items": [ { "productId": "...", "productName": "...", "quantity": "...",
               "unitPrice": "...", "totalPrice": "...", "taxAmount": "..." } ],
  "shippingAddress": { "recipientName": "...", "country": "GB", "...": "..." },
  "shippingOption": { "code": "express", "name": "Express delivery" },
  "shippingPrice": 15.0,
  "subtotal": 10.0,
  "taxTotal": 5.0,
  "discountTotal": 0.0,
  "taxRate": 0.2,
  "totalAmount": 30.0
}
```

(The lines of that run are not recorded; the parts are. 5.0 of tax is 2.0 on the goods and 3.0 on
delivery, at 20%.)

| Refusal | Status | Same as checkout |
| :--- | :--- | :--- |
| `shippingOption` empty or not offered | `400`, `errors.ShippingOption` ("'teleport' is not a delivery option. Offered: standard, express.") | yes - one shared rule |
| The cart is empty | `409` | yes |
| No `addressId` and no default address | `409` | yes |
| `addressId` not the caller's, or missing | `404` "Delivery address not found." | yes |
| A product not currently for sale | `409` "Not currently for sale: ..." | yes |
| Catalog, Cart or Identity unreachable | `503` | yes |

A quote is not a reservation: the cart, a price or the address can change before the order, and the
order is then priced again by the same code (`CheckoutPricing`).

## Endpoints the pages use (unchanged)

| Request | Page | Notes |
| :--- | :--- | :--- |
| `GET /api/orders/shipping-options` - anonymous | checkout | `[{ code, name, price }]` |
| `POST /api/orders` `{ addressId, shippingOption }` | checkout | `200` with the order, status `Submitted`; the page navigates to `/orders/{orderId}` |
| `GET /api/orders/{id}` | order | own orders only; another customer's is `404`, like a missing one |
| `GET /api/orders?page=&pageSize=` | orders | newest first; `{ items, page, pageSize, totalCount }`; the page asks for 10 |
| `GET /api/addresses` | checkout | Identity, specs/017 |

## Bruno

- `order/checkout quote` (new, `seq: 2`): status 200 and the parts add up; stores `quoteTotal`.
- `order/checkout` (now `seq: 3`): also asserts "charged exactly what the quote said".
- `get my orders`, `get order by id`: re-sequenced to 5 and 4.
