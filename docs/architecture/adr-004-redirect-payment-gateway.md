# ADR 004: Paying Through a Redirect Gateway (VNPay)

* **Status**: Accepted
* **Deciders**: the project owner, with the recommended options
* **Date**: 2026-10-03
* **Issue**: #289; design record [specs/143-vnpay-sandbox](../../specs/143-vnpay-sandbox/)

---

## 1. Context

Since specs/002 the saga has asked Payment to charge, and Payment has answered at once. `StubPaymentGateway` read a
setting and approved or refused without contacting anyone. That shape matches a card processor called
server-to-server, but not how people in Vietnam pay online. They use a **redirect gateway**: VNPay, MoMo, ZaloPay and
others.

1. The shop sends the customer's browser to the gateway's page, with a link it has signed.
2. The customer pays there, or cancels.
3. The gateway tells the shop by calling it directly: the **IPN** (Instant Payment Notification), signed with a secret
   the two share.
4. The gateway sends the browser back to the shop's return page.

Three things are new:
- the outcome arrives minutes later, from outside;
- the address it arrives at is public;
- the browser's return carries the outcome too, and anyone can type it.

## 2. Decision

**Payment gains a second provider behind the same seam. A redirect gateway opens a *checkout* when the saga asks, and
decides nothing. The gateway's signed IPN decides, once. The saga, the contracts and Order do not change.**

```mermaid
sequenceDiagram
    autonumber
    participant S as Saga
    participant P as Payment
    participant B as Browser
    participant V as VNPay
    S--)P: ProcessPaymentCommand
    P->>P: payment_checkouts row (no decision, nothing published)
    B->>P: GET /api/payments/orders/{id}/checkout (owner)
    P-->>B: payUrl, signed HMAC-SHA512, expires before the saga gives up
    B->>V: open payUrl, pay or cancel
    V->>P: IPN, signed (server to server)
    P->>P: verify signature, merchant, amount; claim checkout; one payments row
    P--)S: PaymentProcessedEvent / PaymentFailedEvent (same transaction)
    V-->>B: redirect to /payment/vnpay-return?vnp_TxnRef=...
    B->>B: show the order as the shop records it
```

- **A checkout, not a pending payment.** `payments` stays immutable once written, with one row per order: the
  guarantee that makes replays safe (specs/002). A waiting checkout is a different thing, so it gets its own table,
  `payment_checkouts`. A `Pending` status would have been a value an earlier image cannot read.
- **The signature is the IPN's whole authority.** Checked first, in fixed time, before any order is looked at. Then
  the merchant code, then the amount, to the unit.
- **Exactly once.** A guarded `UPDATE ... WHERE "CompletedAt" IS NULL` decides which copy of a notification records
  the payment. The payment's uniqueness on `OrderId` backs it up. The record, the audit entry and the saga's reply
  commit in one transaction (Principle III). Every later copy is answered `02`, *already confirmed*.
- **The browser's word is never taken.** The return page reads only the order reference, and shows the order as the
  shop records it.
- **The window is shorter than the saga's patience.** The link expires (`vnp_ExpireDate`, 8 minutes by default)
  before the saga's payment timeout (10 minutes). Payment refuses to start otherwise. A payment that somehow arrives
  later is refunded by the existing late-payment path (specs/053).
- **Dong only.** VNPay settles in VND, and the shop converts nothing (specs/022). An order in another currency is
  refused like a declined card. It is never handed to the stub.
- **A simulator, written separately.** Development and CI pay through `Ecommerce.VnPaySimulator`, which speaks the
  same protocol with its *own* signing code. A link Payment signs wrongly is refused there, as VNPay would refuse it.
- **The stub stays the default**, and stays honest. `/health` says `movesMoney: false` for the stub and for VNPay's
  sandbox, and the checkout keeps telling customers so.

## 3. Consequences

**Better**
- The shop takes payment the way its customers pay, and the code is ready for VNPay's sandbox and then production:
  settings only, no code.
- The saga needed no change. Waiting for a reply was always its design (the payment timeout, the late-payment refund).
  A redirect gateway only makes the wait longer.
- CI exercises paying and cancelling end to end on every change, through the simulator.

**Worse, accepted**
- **Refunds are still recorded, not sent.** VNPay's refund API needs merchant credentials that cannot be tested
  without a merchant account. A refund is recorded as owed, and a person makes it in VNPay's merchant portal.
- **One attempt per order.** A customer who cancels at VNPay places the order again, as after a declined card.
- **A dollar price list cannot be paid through VNPay.** Choosing payment methods per order would lift this, at the
  cost of a contract change through the saga.
- **The real sandbox is the owner's step.** It needs a merchant registration, which no code can do.

## 4. Alternatives considered

| Alternative | Why not |
| :-- | :-- |
| `PaymentStatus.Pending`, updated by the IPN | Breaks "a payment is immutable once written", and adds a status an earlier image cannot read |
| Trust the return page's `vnp_ResponseCode` | Anyone can type that address. A free order |
| Have the saga wait for a new "customer paid" event | Changes a contract every relaying service must be rebuilt for (CLAUDE.md's gotcha). The existing reply events already mean exactly that |
| Fall back to the stub for non-VND orders | A stand-in would fulfil real orders unpaid |
| Test against VNPay's sandbox in CI | Needs a merchant account, the network, and a public IPN address. The simulator gives the same protocol, offline |
| Reference Payment's signing code from the simulator | Both sides would share any bug. Separate code is an independent check |
