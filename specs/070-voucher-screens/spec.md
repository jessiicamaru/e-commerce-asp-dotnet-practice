# Feature Specification: Vouchers (part 2 - the screens)

**Feature Branch**: `070-voucher-screens` | **Created**: 2026-09-26 | **Issue**: #108 (closes it)
**Builds on**: [specs/069-vouchers](../069-vouchers/), the server, merged in #153.

## Why

Since #153 the server prices, claims and releases vouchers, but no page lets anybody make one or type one in.

## User Scenarios

### US1 - A customer applies codes at checkout (P1)

The checkout summary has a voucher box.
- **Apply** checks the code with the server, through the same quote the summary shows.
  - A valid code joins the list of applied codes, and the totals follow.
  - A refused code is shown in the server's words **beside the box**, and the rest of the summary stays as it
    was.
- Each applied code can be removed.
- Placing the order sends the applied codes.
- The summary shows each voucher and what it took off. A line with a discount says so.
- The order page shows the same after the order is placed.

**Acceptance**:
1. Applying a code asks for the quote with that code added. If the quote is refused, the code is not kept and
   the reason is shown.
2. The order is placed with exactly the applied codes.
3. The totals list each voucher by code, with the shop's name for a shop voucher.

### US2 - A seller manages their shop's vouchers (P1)

`/shop/vouchers`, in the shop console menu, lists the seller's vouchers:
- the code and name;
- what each gives, in words, for example "30% off, up to 200,000₫, on orders from 1,000,000₫";
- how many uses it has had, against its limit;
- its dates and status.

A dialog creates one:
- the code and a name;
- **percent off** or **an amount off**;
- the amounts for each currency (at least one, never converted);
- the start and end dates, the total limit and the limit per customer;
- the conditions: first order in this shop, and a minimum number of items;
- optionally, **some of the seller's own products**.

**Disable** asks for confirmation first. A refusal from the server is shown in its own words.

### US3 - An administrator manages platform vouchers (P2)

`/admin/vouchers` is the same page for an administrator, in the admin menu (Admin only).
- It adds **free delivery** as a benefit and **new customers** as a condition.
- Products are picked from the whole catalogue.

## Requirements

- **FR-001** Checkout and the quote carry the applied codes. The summary and the order page show the vouchers
  and each line's discount.
- **FR-002** One voucher page and one creation form, shared by the seller and the administrator. It offers
  only what the server accepts from that role.
- **FR-003** Every word is in Vietnamese and English.
- **FR-004** Vitest tests cover:
  - what the checkout asks for and sends;
  - how a refused code is shown;
  - what the form sends for each role;
  - how a voucher is described;
  - disabling.

## Out of scope

- A list of the vouchers a customer could use.
- Editing a voucher.
- Category and variant targets from the screens. Variant targets remain available through the API.
