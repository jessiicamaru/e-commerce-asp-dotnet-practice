# Data Model: Pay with VNPay (sandbox)

## `payment_checkouts` (new, Payment)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | uuid | v7 |
| `OrderId` | uuid | **unique**: one checkout per order |
| `UserId` | uuid | the owner, from `ProcessPaymentCommand`; the read is scoped to it |
| `Amount` | numeric(18,2) | what the gateway must report |
| `Currency` | varchar(3) | VND |
| `Provider` | varchar(32) | `VnPaySandbox` / `VnPay` |
| `Reference` | varchar(64) | `vnp_TxnRef`, the order id in `N` format; **unique** |
| `OpenedAt` | timestamptz | |
| `ExpiresAt` | timestamptz | `OpenedAt` + the payment window |
| `CompletedAt` | timestamptz null | set once, by the guarded claim |
| `ResponseCode` | varchar(8) null | `vnp_ResponseCode` |
| `ProviderReference` | varchar(64) null | `vnp_TransactionNo` |

## `payments` (changed)

| Column | Change |
| :-- | :-- |
| `ProviderReference` | **new, nullable** varchar(64): the gateway's transaction number; null for the stub and for every row before |

**Migration** `AddPaymentCheckouts`: creates one table and adds one nullable column. It is expand-only, so an earlier
image ignores both.

## A checkout's life

```text
ProcessPaymentCommand ──(VNPay)──► opened (CompletedAt null)
                                     │ IPN, signed, right amount
                                     ├─ 00/00 ──► completed, payment Approved ──► PaymentProcessedEvent
                                     └─ other ──► completed, payment Rejected ──► PaymentFailedEvent
ProcessPaymentCommand ──(not VND)──► no checkout, payment Rejected ──► PaymentFailedEvent
```

A checkout that is never completed simply stays open. The saga's timeout fails the order. The row is harmless and is
kept as a record of the attempt.
