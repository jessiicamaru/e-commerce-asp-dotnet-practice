# Feature Specification: A seller can see what they sold

**Feature branch**: `034-seller-sales`
**Created**: 2026-09-23
**Status**: Draft
**Closes**: #75

## What is wrong

A seller can list a product, price it, stock it and photograph it. When a customer buys it, **the
seller cannot find out.** Nothing records that the sale was theirs: an order line keeps the product,
the variant, the SKU, the name, the options, the price and the tax, and not whose product it was.
Every page and every endpoint a seller can reach is about listings; none is about sales.

The marketplace therefore stops at the moment it matters. Four features built a seller who can
prepare something to sell, and the fifth thing a seller needs, knowing it sold, is missing.

## User Scenarios

### US1 - The sale remembers whose it was (P1)

When a customer checks out, each line records the seller whose product it was, as it stood at that
moment, and keeps it.

**Why this priority**: everything else reads it. Without it there is nothing to show.

**Acceptance**
1. A line for a seller's product records that seller.
2. A line for the shop's own product records no seller, exactly as the catalogue does.
3. What is recorded does not change if the product changes hands or is deleted afterwards.
4. An order holding products of two sellers and of the shop records each line's own seller.

### US2 - A seller lists their sales (P1)

A seller asks for their sales and sees every order that contains at least one of their lines, newest
first, a page at a time. Each row says when it was placed, where it has got to, how many of their
lines and units it holds, and what **their** lines came to.

**Acceptance**
1. A paid order holding one of their lines appears.
2. An order holding none of their lines does not, even if it holds another seller's.
3. A failed order never appears, and neither does one still being settled.
4. What a row reports is about their lines only - not the order's total, which includes goods that
   are not theirs, delivery and tax.
5. The request names no seller; the seller is whoever signed in.

### US3 - A seller opens one sale (P1)

**Acceptance**
1. It shows their lines, what each was bought as, how many, at what price, and where the order has got to.
2. It does **not** show another seller's lines, the order's total, the customer, or where it is being delivered.
3. An order that holds none of their lines answers **exactly** like an order that does not exist,
   and so does one that failed or is still settling.

### US4 - The storefront gives them somewhere to click (P2)

A seller reaches their sales from their shop page, reads the list, and opens a sale.

**Acceptance**
1. The shop page links to the sales.
2. The list and the sale are readable in both of the shop's languages.
3. Amounts are shown in the currency the order was charged in, not the one being browsed in.
4. A refused sale is shown as the server words it.

### Edge cases

- **Two sellers and the shop on one order.** Each seller sees their own lines and their own
  subtotal; neither learns the other exists.
- **A product deleted after it sold.** The sale is still theirs; the line froze the seller.
- **An order placed before this feature.** Its lines record no seller and it is nobody's sale -
  including the seller who did sell it. There is no way for Order to learn it afterwards: the answer
  lives in another service's database. Accepted and said out loud.
- **A Catalog older than this feature** answering the pricing question. It cannot say whose a
  product is, and "cannot say" must not be silently written down as "the shop's".

## Requirements

- **FR-001** At checkout, each order line MUST record the seller of the product being bought, as the
  catalogue states it at that moment, and MUST keep it unchanged afterwards.
- **FR-002** A line for a product with no seller MUST record no seller.
- **FR-003** When the catalogue cannot say whose a product is, the order MUST still be placed, the
  line MUST record no seller, and the fact MUST be logged as a warning. Refusing a customer's order
  over a reporting field would be the wrong trade.
- **FR-004** A seller MUST be able to list the orders holding at least one of their lines, newest
  first, paged, with per-order totals computed over their lines only.
- **FR-005** A seller MUST be able to read one such order, showing their lines only.
- **FR-006** Only orders that were paid - including those since prepared or shipped - MUST count
  as sales. Failed and still-settling orders MUST NOT.
- **FR-007** Neither read may disclose the customer, the delivery address, the order's total, or
  any line that is not the caller's.
- **FR-008** An order that is not the caller's sale MUST be refused identically to one that does not
  exist.
- **FR-009** Both reads MUST identify the seller from the token and accept no seller from the request.
- **FR-010** Both reads MUST be refused to anybody who is not a seller.
- **FR-011** The storefront MUST offer both, reachable from the seller's shop page, in both
  languages, with amounts in the order's own currency, and MUST ship with unit tests.

## Out of scope

- **A seller preparing or shipping their part.** Fulfilment stays with an administrator. Doing it
  per seller changes where fulfilment state lives, because an order with two sellers can be half
  shipped - a feature of its own, and the one that would justify showing a seller the address.
- **Attributing orders placed before this feature.** See edge cases.
- **Money owed to a seller**, commission, payouts.
- **The customer seeing which shop sold each line.** Nothing stops it later; nothing asks for it now.

## Success Criteria

- **SC-001** A seller's product, bought by a customer and paid for, appears in that seller's sales
  and in no other seller's.
- **SC-002** On an order mixing two sellers and the shop, each seller's view shows only their lines
  and a subtotal equal to the sum of those lines.
- **SC-003** Asking for a sale that is not the caller's produces a response indistinguishable from
  asking for one that does not exist.
- **SC-004** A failed order is never reported as a sale.
- **SC-005** A customer is refused, and an anonymous caller is refused.

## Assumptions

- The seller of a product is what the catalogue says at checkout. A product that changes hands
  later leaves earlier sales where they were.
- "The shop itself" (no seller) has no sales page here; an administrator already sees every order
  through fulfilment.
