# Feature Specification: Shop applications

**Feature Branch**: `044-shop-applications` | **Created**: 2026-09-24 | **Issue**: #89

## Why

Anybody could open a shop and start listing products in the same request that created their account. A
marketplace needs to check who it lets sell.

## User Scenarios

### US1 - Somebody asks to sell (P1)

A signed-in customer fills in a shop name, what they will sell, and a phone number, and sends an
application. Registering through `register-seller` does the same thing in one step for somebody new: it
creates an account and an application. Until the application is decided, the person is a customer and
nothing more. They can see where their application stands.

**Acceptance**
1. `register-seller` returns roles `["Customer"]`, opens no shop, and tells Catalog nothing.
2. While one application is waiting, a second is refused (409).
3. A person who already sells cannot apply (409).

### US2 - Staff decide (P1)

A moderator or administrator works the queue, oldest first. Approving grants Seller, opens the shop, and
tells Catalog, all at once. Rejecting records a reason, which the applicant reads, and they may apply
again.

**Acceptance**
1. After approval, the applicant's next sign-in or refresh carries Seller, and the shop name reaches the
   catalogue.
2. Two staff approving at once open one shop. The second approval is 409.
3. A customer or a seller cannot see the queue or decide (403).
4. The applicant is notified of the decision.
5. The decision is recorded in the audit log under Moderation.

## Requirements

- **FR-001**: At most one pending application per person, enforced by the database.
- **FR-002**: A decision moves an application out of Pending exactly once. Everything the decision does
  commits with it.
- **FR-003**: The queue shows the applicant's name and email to staff only.
- **FR-004**: Shops that exist today keep selling as they are, with no application needed.

## Success Criteria

- **SC-001**: In one Bruno run, a new seller is refused Seller-only endpoints until approved, and
  approving twice is 409.
- **SC-002**: With five approvals fired at once, exactly one succeeds.
