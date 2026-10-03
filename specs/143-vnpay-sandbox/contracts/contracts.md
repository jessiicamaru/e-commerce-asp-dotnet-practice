# Contracts: Pay with VNPay (sandbox)

No message or gRPC change. The saga's `ProcessPaymentCommand`, `PaymentProcessedEvent` and `PaymentFailedEvent` are
untouched.

## `GET /api/payments/orders/{orderId}/checkout` (signed in, the order's owner)

```json
{
  "provider": "VnPaySandbox",
  "state": "AwaitingPayment",
  "payUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=...&vnp_SecureHash=...",
  "expiresAt": "2026-10-03T10:08:00Z"
}
```

`state` is one of:

| State | Meaning |
| :-- | :-- |
| `Preparing` | no payment and no checkout yet, as far as Payment knows |
| `AwaitingPayment` | open, not expired: `payUrl` is set |
| `Expired` | open, past `expiresAt` |
| `Paid` | a payment row, approved |
| `Failed` | a payment row, rejected |

`provider` is the gateway Payment uses: `Stub`, `VnPaySandbox` or `VnPay`. With the stub, the state goes from
`Preparing` straight to `Paid` or `Failed`, and there is never a `payUrl`.

An order that is not the caller's, or that Payment has never heard of and that has no owner, answers `Preparing` only
to its owner. Payment knows the owner only once the saga has asked it to charge. Until then the answer is
`Preparing` for anybody, and carries no data. Once the owner is known, anyone else gets 404.

## `GET /api/payments/vnpay/ipn?vnp_...` (anonymous; called by the gateway)

Always HTTP 200 with:

```json
{ "RspCode": "00", "Message": "Confirm Success" }
```

| RspCode | When |
| :-- | :-- |
| `00` | recorded (approved or rejected) |
| `01` | no checkout with that reference |
| `02` | already confirmed |
| `04` | the amount differs |
| `97` | bad signature, or another merchant's code |
| `99` | unexpected; retried by the gateway |

## The pay URL (VNPay 2.1.0)

```text
vnp_Version=2.1.0 vnp_Command=pay vnp_TmnCode vnp_Amount=<VND x 100> vnp_CurrCode=VND vnp_TxnRef=<order id N>
vnp_OrderInfo vnp_OrderType=other vnp_Locale=vn|en vnp_ReturnUrl vnp_IpAddr vnp_CreateDate vnp_ExpireDate
vnp_SecureHash=HMACSHA512(secret, sorted urlencoded query)
```

Dates are `yyyyMMddHHmmss` in Vietnam time (UTC+7).

## `/health` (Payment)

The response gains `movesMoney` (boolean).

| Field | Stub | VNPay sandbox |
| :-- | :-- | :-- |
| `provider` | `Stub - no money is moved` | `VnPay sandbox - no money is moved` |
| `configuredOutcome` | `Approve` / `Reject` | `Customer` |

## Settings

| Setting | Default | Meaning |
| :-- | :-- | :-- |
| `PAYMENT_PROVIDER` | `Stub` | or `VnPay` |
| `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET` | none (required with VnPay) | the merchant's; the simulator's in development |
| `VNPAY_PAY_URL` | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` | the simulator's address in development |
| `VNPAY_RETURN_URL` | `${STOREFRONT_URL}/payment/vnpay-return` | where the gateway sends the customer back |
| `VNPAY_PAYMENT_WINDOW_MINUTES` | 8 | must be shorter than the saga's payment timeout |
| `VNPAY_LIVE` | `false` | true only for the production gateway: the rows say `VnPay` and `movesMoney` is true |

The simulator (`server/src/Tools/Ecommerce.VnPaySimulator`, port 5064) reads `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET` and
`VNPAY_SIMULATOR_IPN_URL`.

## Storefront

- The order page shows **Pay with VNPay** while the checkout is `AwaitingPayment`. The button is a full-page link to
  `payUrl`.
- `/payment/vnpay-return?vnp_TxnRef=...` sends the customer to `/orders/{id}`. It reads only the reference, never the
  response code.
