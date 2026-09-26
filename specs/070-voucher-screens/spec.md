# Feature Specification: Vouchers (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature Branch**: `070-voucher-screens` | **Created**: 2026-09-26 | **Issue**: #108 (closes it)
**Builds on**: [specs/069-vouchers](../069-vouchers/), the server, merged in #153.

**Status**: Merged (#154, 2026-09-26). Client only.

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

**Why this priority**: A voucher nobody can type in discounts nothing; this is the customer's half of the feature.

**Independent Test**: Render the checkout with a quote that accepts one code and refuses another; applying the first
calls the quote with it and keeps it as a chip, applying the second shows the refusal beside the box without changing
the summary, and placing the order sends exactly the kept code (`pages/checkout/index.test.tsx`).

**Acceptance Scenarios**:

1. **Given** the checkout summary, **When** the customer applies a code the server accepts, **Then** the quote was asked
   with the applied codes plus that one, and the code becomes a removable chip.
2. **Given** a code the server refuses with 409, **When** it is applied, **Then** the server's words appear beside the
   box, the code is not kept, and the summary is unchanged.
3. **Given** applied codes, **When** one is removed, **Then** the summary is quoted again without it.
4. **Given** applied codes, **When** the order is placed, **Then** `voucherCodes` carries exactly them.
5. **Given** an order with a shop voucher and a platform voucher, **When** its page is opened, **Then** each voucher is
   listed by its code - the shop voucher with the shop's name - with its amount, and each discounted line shows its
   discount.
6. **Given** several codes, **When** the quote is asked, **Then** the query repeats `voucherCodes=` once per code, the
   way the server binds a list.

---

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

**Why this priority**: Sellers fund their own vouchers (specs/069 research D1); without this page they cannot run a
campaign at all.

**Independent Test**: Render `/shop/vouchers`: vouchers listed in words; the create dialog offers no free delivery and
no "new customers", picks only from the seller's own products, and sends a shop voucher; disabling asks first
(`pages/shop-vouchers/index.test.tsx`).

**Acceptance Scenarios**:

1. **Given** a seller's vouchers, **When** the page loads, **Then** each is described in words per currency, with uses
   against limits, dates and status.
2. **Given** the create dialog as a seller, **When** it opens, **Then** free delivery and "new customers" are not
   offered, and products are searched among the seller's own (`Product.list` is not called).
3. **Given** a filled form, **When** it is sent, **Then** the request carries the benefit, amounts per currency,
   limits, conditions and targets the form shows - and no owner.
4. **Given** the server refuses (400 or 409), **When** the form is sent, **Then** its words appear inside the form.
5. **Given** an active voucher, **When** Disable is pressed, **Then** nothing is sent until the confirmation.

---

### US3 - An administrator manages platform vouchers (P2)

`/admin/vouchers` is the same page for an administrator, in the admin menu (Admin only).
- It adds **free delivery** as a benefit and **new customers** as a condition.
- Products are picked from the whole catalogue.

**Why this priority**: P2 because platform campaigns could already be made through the API (Bruno does), and the page
is the seller's page with three differences.

**Independent Test**: Render `/admin/vouchers`: free delivery and "new customers" are offered and the picker searches
the whole catalogue (`AdminVouchersPage` test).

**Acceptance Scenarios**:

1. **Given** an administrator, **When** the create dialog opens, **Then** free delivery and "new customers" are offered,
   and "first order in my shop" is not.
2. **Given** an administrator, **When** they search products, **Then** the whole catalogue is searched.
3. **Given** a moderator, **When** the console menu is drawn, **Then** "Vouchers" is not in it.

### Edge Cases

- **A free-delivery voucher** names no products: the form sends no targets for it.
- **Dates** entered in the form are sent as ISO instants.
- **A fixed amount** sends its value and no cap; a minimum quantity is a condition, not a field.
- **An order from before vouchers** has no `vouchers`; the totals fall back to the single discount row.

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
- **FR-005** No page names an owner: the server reads whose voucher it is from the token.

### Key Entities

- **Applied voucher (as drawn)**: code, name, whether it is a shop's (and which), benefit and amount - from the quote or
  the order.
- **Voucher summary (as listed)**: the server's `VoucherSummary`.
- **New voucher (as sent)**: the form's request, built by `toNewVoucher`.

## Success Criteria

- **SC-001**: A code refused by the server never enters the summary - `shows a refused code in the server words and
  keeps the summary as it was`.
- **SC-002**: The order is placed with exactly the applied codes - mutation-checked.
- **SC-003**: Each role is offered only what the server accepts - 8 of 8 mutations caught, including "free delivery
  offered to a seller" and "a seller picks from the whole catalogue".
- **SC-004**: Storefront suite 399/399 in 67 files; Bruno through the storefront 229/229.

## Assumptions

- The server's 409 words are fit to show a customer (specs/069 wrote them for that).
- The quote is cheap enough to ask once more per Apply.

## Out of scope

- A list of the vouchers a customer could use.
- Editing a voucher.
- Category and variant targets from the screens. Variant targets remain available through the API.
