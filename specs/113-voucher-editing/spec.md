# Feature Specification: A voucher's terms can be corrected

**Feature Branch**: `113-voucher-editing` | **Created**: 2026-10-01 | **Issue**: #219

**Status**: Draft

**Input**: Issue #219 - "a voucher cannot be edited": "Edit what does not change a past order's price: end date, usage
limits, and the conditions' minimums - never the amount taken off once used. Audited with before and after."

## Why

A seller or administrator who mistyped a voucher's end date or its usage limit must disable it and create another, with
a new code the shoppers have not seen - the code they printed, posted or emailed stops working. Every order freezes what
its vouchers took off (specs/069), so the terms that decide *whether* a voucher applies can change without touching any
order already placed; the terms that decide *how much* it takes off are the promise the shopper saw, and stay.

## User Scenarios & Testing *(mandatory)*

### US1 - Correct a voucher's dates, limits and minimums (Priority: P1)

A seller opens their vouchers, picks an active one, and changes its name, end date, total limit, per-customer limit, a
currency's minimum subtotal or its minimum quantity. The same code keeps working with the new terms.

**Why this priority**: It is the issue.

**Acceptance Scenarios**:

1. **Given** an active voucher used by one order, **When** its end date is extended, **Then** it applies until the new
   date, and the order already placed keeps exactly what it used.
2. **Given** a voucher used 3 times, **When** its total limit is set to 5, **Then** two more uses are allowed; set to 2,
   **Then** 409 and nothing changes - a limit cannot go below the uses already made.
3. **Given** a voucher with a minimum subtotal in VND, **When** the minimum is lowered, **Then** a checkout that did not
   qualify now does, priced by the same code as the order.
4. **Given** the edit, **Then** the audit log holds a `VoucherEdited` entry with the terms before and after.
5. **Given** a disabled voucher, **Then** 409: a disabled voucher stays disabled and is not edited.

---

### US2 - Only the owner edits (Priority: P1)

**Acceptance Scenarios**:

1. **Given** another seller's voucher, **When** a seller edits it, **Then** 404 - the same as a voucher that does not
   exist (specs/027).
2. **Given** a seller's voucher, **When** an administrator edits it, **Then** 404: an administrator edits the platform's
   vouchers; a seller's voucher is paid for by that seller (specs/069), so its terms are the seller's to set. An
   administrator can still disable one.
3. **Given** a customer, **Then** 403; anonymous, 401.

---

### US3 - The amount is never edited (Priority: P1)

The benefit, the percentage, the fixed amount, the cap, the currencies, the targets, the code and the start date are
not part of the edit at all - the request has no field for them.

### Edge Cases

- **An edit racing a checkout's claim**: the total limit is written by one guarded statement that holds only while the
  uses so far fit under it, so a claim and an edit on one voucher serialise on its row; neither can leave more uses than
  the limit.
- **An end date in the past**: refused (400) - ending a voucher now is what disabling is for, and it says so.
- **A per-customer limit below a customer's uses**: allowed - that customer simply cannot use it again. Only the total
  is checked against the uses, because only the total has a count on the voucher.
- **A currency the voucher is not priced in**: refused (400); currencies are not added by an edit (a voucher without an
  amount in a currency is not usable in it, specs/069).
- **Two editors at once**: the last one wins, and the audit log shows both.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `PUT /api/vouchers/{id}` (Seller, Admin) with `name`, `endsAt`, `totalLimit`, `perCustomerLimit`,
  `minSubtotals` (per currency the voucher has) and `minQuantity` changes those terms and answers the voucher.
- **FR-002**: Only an active voucher is edited; a total limit below the uses made is 409; the check and the write are
  one statement.
- **FR-003**: A seller edits only their own vouchers, an administrator only the platform's; anything else is 404.
- **FR-004**: The edit is audited (`Order` / `VoucherEdited`, before and after) in the transaction that makes it.
- **FR-005**: `/shop/vouchers` and `/admin/vouchers` offer "Edit" on an active voucher, prefilled with its terms.

## Success Criteria *(mandatory)*

- **SC-001**: A voucher's end date is extended and its code keeps working; orders placed before keep what they used
  (tested).
- **SC-002**: No edit lets the uses exceed the total limit, including an edit and claims at once (tested).
- **SC-003**: The request cannot change what a voucher takes off - there is no field for it (the contract).

## Assumptions

- Correcting what a voucher takes off means disabling it and creating another; the amount is the promise shoppers saw.
- The start date is not edited: a voucher that has not started is disabled and recreated as easily, and one that has
  started has been seen.
- No notice is sent: nobody holds a voucher until they use it.
