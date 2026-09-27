# Feature Specification: Sellers say where their payouts go

**Feature Branch**: `106-payout-accounts` | **Created**: 2026-09-27 | **Issue**: #213

**Status**: Draft

**Input**: Issue #213 - "sellers have nowhere to receive their payouts".

## Why

The shop works out what it owes each seller and records payouts (specs/037), but it does not know where to send the
money. `SellerProfile` holds a shop name and a description and nothing else, and
`grep -ri "BankAccount|Iban|AccountNumber" server/src` finds nothing. An administrator recording a payout has no account
to pay into, and the payout records nothing about where it went.

## User Scenarios & Testing *(mandatory)*

### US1 - A seller gives a payout account (Priority: P1)

A seller enters their bank, the account holder's name and the account number on their payouts page. Afterwards they
see it with the number masked (`•••• 4321`). Changing it emails them, because a changed account is how payout fraud
starts.

**Why this priority**: Without it, no payout has a destination.

**Acceptance Scenarios**:

1. **Given** a seller, **When** they save an account, **Then** reading it back shows the bank, the holder and a masked
   number, never the full one.
2. **Given** a saved account, **When** it is changed, **Then** the seller is emailed. The audit entry carries the
   masked number, never the full one.
3. **Given** a customer who does not sell, **Then** 404 (as for a shop name, specs/027).
4. **Given** an account number that is not 6-34 letters or digits, **Then** 400.

---

### US2 - A payout is made to an account, and records it (Priority: P1)

An administrator sees each seller's account next to what is due. The full number is shown because they must transfer
to it, and it is shown to administrators only. A seller without an account is marked "no payout account" and cannot be
paid. Recording a payout freezes where it went: bank, holder and the last four digits.

**Why this priority**: The payout ledger should say where each payout went, and a changed account must be visible
when paying.

**Acceptance Scenarios**:

1. **Given** a seller with money due and no account, **When** an administrator records a payout, **Then** 409 and
   nothing is claimed.
2. **Given** an account, **When** a payout is recorded, **Then** the payout shows "to Vietcombank · NGUYEN VAN A ·
   ••••4321", and so does the seller's payout list.
3. **Given** an account changed in the last 7 days, **Then** the due list says so next to it.
4. **Given** a moderator, **Then** the full numbers are refused (403).

### Edge Cases

- **Identity unreachable when paying**: the payout is refused (503), not recorded without a destination.
- **The account changes after a payout**: the payout keeps what it froze.
- **Payouts from before this** have no destination recorded, and none is invented.
- **Secrets in the log**: the audit snapshot and the email show only the last four digits.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Identity `seller_payout_accounts` (one per seller): bank name, account holder, account number, updated
  at.
- **FR-002**: `GET`/`PUT /api/sellers/me/payout-account` (Seller). The read is masked. The write is audited with the
  number masked, and emails `PayoutAccountChanged` to the seller in their language.
- **FR-003**: `GET /api/sellers/payout-accounts?sellerIds=` (Admin) returns the full accounts of those sellers.
- **FR-004**: gRPC `PayoutAccounts.GetPayoutAccount(seller_id)`, served by Identity to a forwarded **Admin** token. It
  returns the bank, the holder, the last four digits and the update time, or not found.
- **FR-005**: Order's `RecordPayoutCommand` reads the account first, outside the claim's transaction. With none it
  answers 409. Otherwise it freezes `payouts.PaidToBank`, `PaidToHolder` and `PaidToAccountLast4` in the claiming
  statement.
- **FR-006**: The storefront: a payout-account form on `/shop/payouts`; accounts, "no payout account" and "changed
  recently" on `/admin/payouts`; the destination on both payout lists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Identity tests cover:
  - set and read (masked);
  - a change emails and audits with no full number;
  - a non-seller gets 404;
  - validation;
  - the admin read is full;
  - the gRPC read answers only an Admin.
- **SC-002**: Order tests: no account means 409 with nothing claimed; a payout freezes the destination.
- **SC-003**: Storefront tests: the seller form sends what was typed; the admin due list marks a seller with no
  account and disables paying them.
- **SC-004**: Bruno covers the seller setting and reading an account; a customer 404; a moderator refused the full
  accounts.
- **SC-005**: Each of these mutations makes a test fail: the read unmasked; the audit with the full number; the payout
  recorded without an account; the gRPC read open to any role.

## Decision

1. **The account lives in Identity** ([research.md](research.md) D1).
2. **Order asks Identity when paying, with the administrator's token**, and freezes a masked copy (D2).
3. **No waiting period, a warning instead** (D3).

## Assumptions

- The payment provider is still a stub. A payout is a ledger entry with a destination, not a transfer.

## Out of scope

- Verifying an account with a bank; several accounts per seller; the shop's own accounts.
