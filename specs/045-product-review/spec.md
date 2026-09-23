# Feature Specification: Product review before sale

**Feature Branch**: `045-product-review` | **Created**: 2026-09-24 | **Issue**: #90

## Why

A seller's product went on sale the moment it was created. Nothing stood between `POST /api/products`
and the public catalogue. A marketplace reviews what third parties put on its shelves.

## User Scenarios

### US1 - Nothing a seller lists is on sale until a moderator looks (P1)

A seller's new product is waiting for review. Its seller and staff can see it. Everyone else cannot: it
is not in the listing or in search, its public address is 404, and checkout cannot buy it. Products the
shop itself lists go on sale straight away.

**Acceptance**
1. A seller's new product is `Pending`. Shoppers cannot see it, and pricing reports it as not for sale.
2. An administrator's product is `Approved` as soon as it is listed.

### US2 - Moderators decide (P1)

A moderator or administrator approves a waiting product, or rejects it with a reason. They can also take
an approved product down, with a reason. The seller is told each time, and can send a rejected product
back for review.

**Acceptance**
1. Approving puts the product on sale. A second approval is a 409.
2. The seller reads the reason in their notification and on their product page.
3. A seller cannot approve anything (403).

### US3 - Changing what a shopper sees goes back to review (P1, decided with the user)

A seller who changes an approved product's name, description or photographs sends it back to review.
It is off the shelf until a moderator approves it again. Changing prices or stock does not.

### US4 - A moderator's dashboard (P2)

The dashboard shows how many products and shops are waiting, each with a link to its queue, and the
moderator's own recent decisions.

## Requirements

- **FR-001**: Everything public and everything sellable asks whether the product is approved.
- **FR-002**: A review decision moves a product exactly once. The audit entry and the notice commit with
  the decision.
- **FR-003**: Products that exist today stay approved.
- **FR-004**: Staff edits do not send a product back to review.

## Success Criteria

- **SC-001**: In one Bruno run, the product goes pending, then approved, then renamed (and back to
  pending), then rejected, then resubmitted.
- **SC-002**: Mutation checks fail if the listing filter, the sellable check, or the resubmit-on-edit
  step is removed.
