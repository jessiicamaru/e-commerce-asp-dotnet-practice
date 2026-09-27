# Research: Sellers say where their payouts go

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #213

---

## D1 - The account lives in Identity

**Decision**: A new table, `seller_payout_accounts`, in Identity, keyed by the seller's user id.

**Rationale**: Identity already owns the seller (`seller_profiles`) and people's personal data (delivery addresses,
specs/011). A bank account is personal data of the same kind. Putting it in Order would spread personal data to a
second service that exists to handle orders.

**Alternatives considered**: a column on `seller_profiles`, rejected because a separate table keeps the sensitive
fields out of every profile read and every `SellerRegisteredEvent`; and Order, rejected for the reason above.

---

## D2 - Order asks Identity when paying, with the administrator's token

**Decision**: Recording a payout first reads the seller's account over a new gRPC call, `PayoutAccounts`, forwarding
the administrator's bearer token. Identity serves it only to the Admin role. Order then freezes the bank, the holder
and the last four digits onto the payout row, in the same statement that claims the parts. The call happens before
that transaction, never inside it.

**Rationale**:

- A payout must record where it went, so the destination has to reach Order at the moment of paying.
- The only other ways to get it there are a read model fed by events, which would put account numbers in outbox rows
  and broker queues, or trusting the client to send it, which breaks the rule that the server decides.
- A live read is the address pattern (specs/011, `AddressReading`): the token decides who may ask.
- The call sits outside the claim's transaction for the specs/031 reason: a row lock is never held across a network
  round trip.

The full number is never frozen in Order. An administrator reads it from Identity to make the transfer.

**Alternatives considered**: a read model in Order, rejected because bank numbers would sit in messages; the client
sending the destination, rejected because the server must decide; and freezing the full number, rejected because it
would put a second copy of a secret in a second database.

Recording a payout gains a synchronous dependency on Identity. If Identity is down, a payout is refused (503), which is
the safe outcome for an administrator's occasional action. Checkout is not affected.

---

## D3 - No waiting period, a warning instead

**Decision**: An account changed in the last 7 days is flagged on the administrator's due list. The seller is emailed
at every change. There is no enforced delay.

**Rationale**: A delay would block every legitimate change, such as a seller switching banks the day before payout.
The two things that stop fraud are the email, which reaches the real owner even when a stolen session made the change,
and a person who sees the warning before transferring. Payouts are recorded by hand by an administrator (specs/037),
so a person always sees the warning.

---

## D4 - Masking

**Decision**: Every seller-facing read, every audit snapshot and the email show `•••• 1234` (the last four
characters). Only `GET /api/sellers/payout-accounts` (Admin) returns the full number. The audit redaction
(`AuditSnapshot`) redacts by property name, and "AccountNumber" does not match its secret words, so the handler masks
the value before recording it. SC-005 mutates that.
