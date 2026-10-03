# Research: Pay with VNPay (sandbox)

## D1. A checkout beside the payment, not a pending payment

**Decision**: a new table, `payment_checkouts`, one row per order waiting at the gateway. The `payments` row is written
only when the outcome is known, exactly as the stub writes it.

**Rationale**:
- `Payment` is documented and tested as immutable once written. Its uniqueness on `OrderId` is what makes replays
  safe.
- A pending row would need updating, and `PaymentStatus` would need a value (`Pending`) that a rolled-back image cannot
  read. CLAUDE.md forbids that kind of change.
- The saga's reply events are unchanged.

**Alternative rejected**: `PaymentStatus.Pending`, updated by the IPN.

## D2. The seam: the gateway either decides or asks for the customer

**Decision**: `IPaymentGateway.Begin(...)` returns either a decision (the stub) or "await the customer" (VNPay).
`ChargeOrderCommandHandler` records and replies for a decision, and opens a checkout for the other. Health reads
`ProviderName`, `MovesMoney` and `HealthOutcome` from the gateway.

**Rationale**: one handler, one place where a payment request is idempotent. The stub path is byte-for-byte the same
behaviour.

## D3. The signature: VNPay 2.1.0

**Decision**:
- sort every `vnp_*` parameter except `vnp_SecureHash` and `vnp_SecureHashType` by key, ordinal;
- URL-encode each value the way VNPay's .NET sample does (`WebUtility.UrlEncode`, spaces as `+`);
- join them as `k=v&...`;
- HMAC-SHA512 with the hash secret, written as lowercase hex.

Verification recomputes over the received parameters and compares in fixed time (`CryptographicOperations.FixedTimeEquals`).

**Rationale**: that is the published 2.1.0 scheme. A fixed-time comparison denies a timing oracle on a public endpoint.

## D4. The reference is the order id

**Decision**: `vnp_TxnRef` is the order id in `N` format (32 hex characters).

**Rationale**:
- One attempt per order, so the order id is unique per merchant as VNPay requires.
- The return page can find the order without a lookup table.
- The reference carries nothing secret.

**Alternative rejected**: a separate random reference. It needs a mapping and buys nothing while there is one attempt
per order.

## D5. The IPN's order of checks and its answers

**Decision**:
1. The signature, then the merchant code: `97`.
2. The checkout by reference: `01`.
3. The amount (`vnp_Amount` / 100 equals the checkout's): `04`.
4. Already completed, or a payment exists: `02`.
5. Otherwise claim the checkout with a guarded `UPDATE`. Insert the payment, approved when `vnp_ResponseCode` and
   `vnp_TransactionStatus` are both `00` and rejected otherwise with the code in the reason. Stage the event and the
   audit, then save once. Answer `00`.
6. A unique violation from a concurrent copy: `02`.
7. Anything unexpected: `99`, logged.

**Rationale**:
- These are VNPay's documented codes. The gateway retries until it reads `00` or `02`.
- Checking the signature first means nothing about the shop's data is revealed to an unsigned caller.

## D6. Only dong

**Decision**: VNPay's provider refuses an order whose currency is not VND. It records a rejected payment ("VNPay settles
in VND only"), so the saga fails the order and releases the stock.

**Rationale**:
- VNPay's `vnp_CurrCode` is VND.
- Converting would break the shop's rule that it converts nothing (specs/022).
- Falling back to the stub would let a stand-in fulfil real orders.

**Consequence**: with VNPay in production, a dollar price list is not payable. That is recorded in the docs as the
owner's choice to make.

## D7. Refunds stay recorded, not sent

**Decision**: cancellations, part cancellations, returns and late payments keep recording refunds in `refunds`, as
today. Nothing calls VNPay's refund API.

**Rationale**:
- The refund API (`merchant_webapi`) needs its own merchant credentials, and cannot be verified without them.
- A refund call that cannot be tested is a path that fails in production first.
- The record already says what is owed. A person refunds it in VNPay's merchant portal.

Follow-up noted in the backlog.

## D8. A simulator, written separately

**Decision**: `server/tools/Ecommerce.VnPaySimulator`, a minimal ASP.NET Core app:
- `GET /paymentv2/vpcpay.html` verifies the URL's signature, merchant and expiry, then shows the amount with **Pay** and
  **Cancel**;
- `POST /paymentv2/complete` signs the IPN parameters, calls the configured IPN address server-to-server, then
  redirects the browser to `vnp_ReturnUrl` with the same parameters.

It has its own copy of the signing code rather than a reference to Payment's.

**Rationale**:
- Separate code is an independent check: a URL Payment signs wrongly is refused by the simulator, as the real gateway
  would refuse it.
- With a project reference, both sides would share the same bug.

**Alternatives rejected**:
- WireMock or a hand-rolled static page: neither can sign an IPN.
- The real sandbox in CI: it needs a merchant account, the network, and a public IPN address.

## D9. The payment window and the saga's timeout

**Decision**:
- `VnPay:PaymentWindowMinutes`, 8 by default, becomes `vnp_ExpireDate`.
- Payment refuses to start when `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS` is also set and the window is not shorter, in
  the same spirit as the orchestrator's own startup check against the reservation TTL.

**Rationale**: a link that outlives the saga's wait lets a customer pay for an order that has already failed. The
late-approval refund (specs/053) covers that case, but it should be rare, not built in.

## D10. Where the money signal lives

**Decision**:
- `/health` gains `movesMoney`: false for the stub and for VNPay with `VnPay:Live` false, which is the default.
- The provider string for the sandbox reads `VnPay sandbox - no money is moved`.
- The storefront's payment notice uses `movesMoney` when present, else the `Stub` prefix.

**Rationale**: the sandbox moves no money either, and the checkout must keep saying so. A provider-name prefix cannot
describe both stand-ins.

## D11. CI: a third branch in the saga job

**Decision**: the saga job already restarts Payment between its approve and reject branches. A third restart runs
Payment with `PAYMENT_PROVIDER=VnPay` and starts the simulator. Then `verify-saga.sh` runs a `vnpay` scenario:
- one order paid through the simulator's form: Paid, stock deducted, nothing held;
- one cancelled: Failed, stock released.

**Rationale**: it reuses the six-service job built for exactly this, so no second stack is needed. The browser flow
through the simulator is verified with Playwright locally and recorded. CI's browser job keeps the stub, which every
other flow relies on.
