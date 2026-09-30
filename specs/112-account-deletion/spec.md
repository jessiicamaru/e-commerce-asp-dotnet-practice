# Feature Specification: A person deletes their account

**Feature Branch**: `112-account-deletion` | **Created**: 2026-10-01 | **Issue**: #217 (part 2 of 2; part 1, the download, is specs/111)

**Status**: Draft

**Input**: Issue #217 - "a person cannot delete their account or download their data". This part is the deletion:
"the account is closed and anonymised; what the shop must keep (orders, for accounting) is kept without the person's
name, email, phone or addresses".

## Why

Vietnam's Decree 13/2023/ND-CP gives a data subject the right to have their personal data deleted. Today nothing lets
a person leave: an account can be locked or banned by staff, never closed by its owner, and every row about them stays
in six databases. specs/111 declared, per service, every table that holds a person's data; this part uses that list
to erase what can be erased and to anonymise what the shop must keep.

## User Scenarios & Testing *(mandatory)*

### US1 - Delete my account (Priority: P1)

A signed-in customer opens their account page, reads what deleting erases and what the shop keeps, types their
password and confirms. They are signed out everywhere at once, and the account is gone: their email can register a new
account, and nothing the shop shows or answers carries their name, email, phone or address any more.

**Why this priority**: It is the right the issue names.

**Acceptance Scenarios**:

1. **Given** a customer with addresses, a finished order, a review, a question, a saved product, a cart, notices and a
   report, **When** they delete their account with the right password, **Then** the answer is 204, every session ends,
   and signing in with the old email and password fails like a wrong password.
2. **Given** that deleted account, **Then** its email registers a new account, which starts empty.
3. **Given** the deleted account's id, **Then** no service's export (specs/111) holds its name, email, phone or any
   address; erased sections are empty; the finished order, its payment and any refund are still there for the shop's
   books, without the delivery address's name, street or phone.
4. **Given** the review and the question, **Then** they stay on the product page without the author's name ("a former
   customer"), and the product's rating is unchanged.
5. **Given** a wrong password, **Then** 400 and nothing changes; it counts toward the sign-in pause (specs/062), like a
   wrong current password when changing it.
6. **Given** an anonymous caller, **Then** 401.

---

### US2 - Nothing is left half-done (Priority: P1)

A person cannot delete their account while the shop still owes them something or they owe somebody something: an order
on its way, a return in progress, or - for a seller - a sale not yet delivered, a return on one of their parcels, or
money not yet paid out.

**Acceptance Scenarios**:

1. **Given** a customer with a paid order whose parcel is not delivered, **When** they try to delete, **Then** 409
   naming `OpenOrders`, and nothing changes.
2. **Given** a customer whose return is requested, accepted, escalated or sent back, **Then** 409 naming `OpenReturns`.
3. **Given** a seller with a part of a paid order not delivered or cancelled, **Then** 409 naming `OpenSales`; with a
   return open on one of their parcels, `OpenReturns`; with earnings not yet claimed by a payout, `UnpaidEarnings`.
4. **Given** several at once, **Then** one 409 names each of them, and the page words each in the reader's language.
5. **Given** Order unreachable, **Then** 503 and nothing changes - an account is never deleted without the check.

---

### US3 - A seller's shop closes with the account (Priority: P2)

A seller with nothing open deletes their account. Their shop closes (off the shelf, like a closure by staff), their
vouchers stop working, their payout account is erased, and the sales and payouts already recorded stay for the books.

**Acceptance Scenarios**:

1. **Given** a seller with listed products, **When** the account is deleted, **Then** the shop is closed with the reason
   "the account was deleted" and none of its products is on the shelf.
2. **Given** the seller's vouchers, **Then** each is disabled.
3. **Given** the seller's payouts, **Then** they stay with bank and last four digits, without the holder's name.

---

### US4 - Staff accounts are not deleted from here (Priority: P3)

An administrator or moderator cannot delete their own account from the account page: 409 `StaffAccount`. A moderator
asks an administrator to revoke the role first; an administrator's account is not closed this way at all.

### Edge Cases

- **Two deletions at once** (two tabs): one wins; the other finds the account already gone (401, its token revoked,
  or 400 on the password) and changes nothing.
- **An order placed while the check runs**: the check and the deletion are not one transaction across two services.
  The window is the length of one gRPC call; the deletion revokes every token, so nothing can follow it. Accepted and
  written down (research D2).
- **A message arriving at a service after the erasure** - a notice for the deleted person published just before: it
  is stored for an id nobody can sign in as, and read by nobody. Accepted.
- **The audit log**: the security record keeps what happened (the entries, their categories and actions) but not the
  person's email in its summaries, their email as an actor, or the snapshots of their profile.
- **A seller's shop name** stays on orders already placed: it was frozen onto each order line at checkout (specs/036)
  as the name of a business, and an order is a record of a purchase.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `DELETE /api/auth/me` with `{ password }` deletes the caller's account (from the token), after checking
  the password and the blockers. 204 on success.
- **FR-002**: Identity asks Order live, over gRPC with the caller's token forwarded, what blocks the caller:
  `OpenOrders`, `OpenReturns`, `OpenSales`, `UnpaidEarnings`. Any → 409 with `code: AccountHasOpenBusiness` and
  `reasons`. Staff → 409 `StaffAccount`. Order unreachable → 503.
- **FR-003**: In one transaction Identity anonymises the user row (placeholder email, empty names, no phone, an
  unusable password, two-factor off, no roles, `DeletedAt` set), deletes every other row it holds about them (the
  tables of its specs/111 inventory), and publishes `AccountDeleted` and `AccessTokensRevoked` through its outbox.
- **FR-004**: Catalog, Order, Cart and Activity consume `AccountDeleted` and erase or anonymise their own rows as each
  service's inventory declares. Payment keeps its rows unchanged: they hold ids and amounts only.
- **FR-005**: Each service's inventory declares every exported section as **erased** or **kept** (with the reason the
  shop keeps it). A test per service holds that declaration to the result: after deletion, erased sections are empty
  and kept sections carry none of the person's name, email, phone or address.
- **FR-006**: The storefront's account page has "Delete my account": what is erased, what is kept, the password, a
  confirmation; a refusal's reasons worded in the reader's language; success signs out and says the account is gone.

### Key Entities

- **A deleted account**: Identity's user row with `DeletedAt` set and nothing personal left in it.
- **`AccountDeleted`**: the message each service erases on - the person's id, the email that was theirs (so the audit
  log can take it out of its summaries), and when.

## Success Criteria *(mandatory)*

- **SC-001**: After deletion the email registers again, and no service's export for the old id holds the planted
  name, email, phone or address (tested in each service).
- **SC-002**: Every table of every model has a declared fate on deletion - no service's test passes with a section
  undeclared.
- **SC-003**: Each blocker refuses the deletion with its own reason and changes nothing (tested per blocker).
- **SC-004**: The deleted account's sessions stop working within seconds, like a ban (specs/065).

## Assumptions

- Deletion is immediate and final: no grace period, no undo. The page says so before the password is asked for.
- No confirmation email: the address is erased in the same transaction, and the page itself confirms.
- Records the shop must keep for its books (orders, payments, refunds, payouts, voucher uses) are kept without the
  person's name, email, phone or delivery address. The country of delivery stays, because the tax charged depends on it.
- Staff deleting another person's account is out of scope.
