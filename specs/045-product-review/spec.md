# Feature Specification: Product review before sale

> Completed on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature Branch**: `045-product-review` | **Created**: 2026-09-24 | **Issue**: #90

**Status**: Merged (#97, 2026-09-23 21:24 UTC - 2026-09-24 in the project's local time)

**Input**: Issue #90, "a seller's product is reviewed before it goes on sale": a seller's new product is
pending review - visible to its seller, invisible in the public catalogue and not sellable at checkout;
a moderator or admin approves it, or rejects it with a reason; a rejected product can be edited and
resubmitted; a moderator can take down an approved product; a moderator console with the product
queue, the shop applications queue and the moderator's recent decisions; notifications to the seller
and audit entries under Moderation; existing products stay approved.

## Why

A seller's product went on sale the moment it was created. Nothing stood between `POST /api/products`
and the public catalogue. A marketplace reviews what third parties put on its shelves.

Specs/027 made the shop a marketplace and specs/044 made opening a shop an application that staff
approve. This feature does the same for what an approved shop lists: the shop checks who sells
(specs/044) and what goes on its shelves (here).

## User Scenarios & Testing *(mandatory)*

### US1 - Nothing a seller lists is on sale until a moderator looks (Priority: P1)

A seller's new product is waiting for review. Its seller and staff can see it. Everyone else cannot: it
is not in the listing or in search, its public address is 404, and checkout cannot buy it. Products the
shop itself lists go on sale straight away.

**Why this priority**: This is the feature. Without it, anything a seller types is on the shelf and in
checkout the moment it is saved, which is what issue #90 reports. Every other story assumes a product
can be held back.

**Independent Test**: As a seller, list a product; as an anonymous shopper, look for it in the listing,
at its address and through checkout pricing; as its seller and as a moderator, look again.

**Acceptance**
1. A seller's new product is `Pending`. Shoppers cannot see it, and pricing reports it as not for sale.
2. An administrator's product is `Approved` as soon as it is listed.

**Acceptance Scenarios** (the same two, and what they imply, in full):

1. **Given** a signed-in seller, **When** they list a product, **Then** it is `Pending`, it is absent
   from `GET /api/products` and from search, `GET /api/products/{id}` is 404 to a shopper, and Catalog's
   pricing answers `sellable = false` for it.
2. **Given** the same pending product, **When** its seller or a moderator or administrator asks for it,
   **Then** they see it, with `reviewStatus: "Pending"`; its seller also sees it in `GET /api/products/mine`.
3. **Given** an administrator, **When** they list a product, **Then** it belongs to the shop itself and is
   `Approved` and on sale at once.
4. **Given** a pending product in a customer's cart, **When** they check out, **Then** checkout refuses
   it with the 409 it already gives an inactive product (`Not currently for sale: ...`).

---

### US2 - Moderators decide (Priority: P1)

A moderator or administrator approves a waiting product, or rejects it with a reason. They can also take
an approved product down, with a reason. The seller is told each time, and can send a rejected product
back for review.

**Why this priority**: US1 without US2 hides every seller's product for ever. The two ship together.

**Independent Test**: List a product as a seller, approve it as a moderator, and confirm it appears to a
shopper and the seller has a notification; reject another with a reason and resubmit it as its seller.

**Acceptance**
1. Approving puts the product on sale. A second approval is a 409.
2. The seller reads the reason in their notification and on their product page.
3. A seller cannot approve anything (403).

**Acceptance Scenarios**:

1. **Given** a pending product, **When** a moderator approves it, **Then** it is `Approved`, in the
   listing and sellable, its seller receives a `ProductApproved` notice linking to
   `/shop/products/{id}`, and a `ProductApproved` audit entry names the moderator as actor.
2. **Given** that product is already approved, **When** anybody approves it again, **Then** the answer
   is 409 and nothing is written - no second audit entry, no second notice.
3. **Given** a pending product, **When** a moderator rejects it with "Photograph the actual camera",
   **Then** it is `Rejected`, `reviewReason` carries that sentence, and the seller's `ProductRejected`
   notice carries it as `reason`.
4. **Given** a rejected product, **When** its seller sends it back, **Then** it is `Pending` again and
   the old reason is gone.
5. **Given** an approved product, **When** a moderator takes it down with a reason, **Then** it is
   `Rejected`, 404 to a shopper and not sellable, and the seller is told why.
6. **Given** a seller, **When** they call approve on anything, **Then** the answer is 403.
7. **Given** a reject or take-down with an empty or whitespace reason, **When** it is sent, **Then** it
   is refused with 400.

---

### US3 - Changing what a shopper sees goes back to review (Priority: P1, decided with the user)

A seller who changes an approved product's name, description or photographs sends it back to review.
It is off the shelf until a moderator approves it again. Changing prices or stock does not.

**Why this priority**: Without it, approval is a one-time gate a seller walks through with a harmless
listing and then rewrites. Decided with the user on 2026-09-24 (issue #90, "Decided with the user").

**Independent Test**: Approve a seller's product, change one of its prices (still approved), then rename
it through a translation (pending, 404 to a shopper).

**Acceptance Scenarios**:

1. **Given** an approved seller product, **When** its seller changes a variant's price, **Then** it
   stays `Approved`.
2. **Given** the same product, **When** its seller sets a translation of its name or description,
   **Then** it is `Pending` and 404 to a shopper.
3. **Given** an approved seller product, **When** its seller uploads or removes the product's
   photograph or a variant's photograph, **Then** it is `Pending`.
4. **Given** an approved product, **When** an administrator makes the same edits, **Then** it stays
   `Approved` - staff edits never send a product back.

---

### US4 - A moderator's dashboard (Priority: P2)

The dashboard shows how many products and shops are waiting, each with a link to its queue, and the
moderator's own recent decisions.

**Why this priority**: The queues work without it; it saves a moderator opening each one to learn
whether there is anything to do. P2 because nothing is lost without it.

**Independent Test**: Sign in as a moderator; the console opens on `/admin/moderation`, the two counts
match the two queues, and a decision just made is at the top of "recently".

**Acceptance Scenarios**:

1. **Given** a moderator, **When** they open `/admin`, **Then** they land on `/admin/moderation`; an
   administrator still lands on the fulfilment queue.
2. **Given** N pending products and M pending shop applications, **When** the dashboard loads, **Then**
   it shows N and M, each linking to its queue.
3. **Given** a moderator has approved a product, **When** they open the dashboard, **Then** that
   approval is among their recent decisions; a customer asking for "my decisions" gets 403.

---

### Edge Cases

- **Two staff decide the same product at once.** The guarded update lets exactly one move it; the other
  gets 409, and only the winner's audit entry and notice are written.
- **The shop's own product is rejected or taken down.** It has no seller, so nobody is notified; the
  audit entry is still written.
- **A seller edits a product that is pending or rejected.** Nothing changes its review state: a pending
  product keeps its place in the queue, a rejected one waits for an explicit resubmit (D3).
- **A seller tries to resubmit somebody else's product.** 404, the same as a missing product
  (`SellerOwnership`, specs/027).
- **A product is resubmitted that is not rejected.** 409, nothing written.
- **A hidden product's id is guessed.** The lookup answers the same 404 as a product that never existed.
- **A pending product already in a cart.** The cart shows it as not for sale (`NotForSale`, from the same
  `sellable` answer) and checkout refuses it with 409.
- **Rollback to an image from before this feature.** That image does not know the column and shows
  pending products publicly (accepted, D1).
- **Not covered at merge, closed later**: the product's photograph address stayed public for a hidden
  product until specs/081 (#166); option translations and adding a variant did not send an approved
  product back until specs/056 (#126).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Everything public and everything sellable asks whether the product is approved.
- **FR-002**: A review decision moves a product exactly once. The audit entry and the notice commit with
  the decision.
- **FR-003**: Products that exist today stay approved.
- **FR-004**: Staff edits do not send a product back to review.
- **FR-005**: A product listed by a seller (who is not also an administrator) MUST start `Pending`; a
  product listed by an administrator MUST start `Approved`.
- **FR-006**: A product that is not approved MUST be visible only to its seller and to staff (Admin,
  Moderator); to anybody else its lookup MUST be indistinguishable from a missing product.
- **FR-007**: Only staff MUST be able to read the review queue and approve, reject or take down.
- **FR-008**: Approve and reject MUST apply only to a pending product; take-down only to an approved one;
  resubmit only to a rejected one. Any other attempt MUST be refused with 409 and write nothing.
- **FR-009**: A rejection and a take-down MUST carry a non-empty reason of at most 500 characters, which
  the seller reads.
- **FR-010**: The product's seller MUST be notified of an approval, a rejection and a take-down.
- **FR-011**: A seller MUST be able to send their own rejected product back for review; an administrator
  may too.
- **FR-012**: A seller changing an approved product's name, description, product photograph or variant
  photograph MUST return it to `Pending`, in the same save as the change. Prices and stock MUST NOT.
- **FR-013**: A seller's own product list MUST include their products in every review state, each with
  its status and reason.
- **FR-014**: Staff MUST be able to read their own recent moderation decisions without being given the
  whole audit log.

### Key Entities

- **Product (review state)**: where a product stands with the moderators - `Approved`, `Pending` or
  `Rejected` - plus the reason the seller reads, when it last entered the queue, when it was last
  decided and by whom.
- **Review decision**: an approve, reject, take-down or resubmit. Not stored as its own row: it is the
  product's new state plus an audit entry (category Moderation) and, for a seller's product, a notice.

## Success Criteria *(mandatory)*

- **SC-001**: In one Bruno run, the product goes pending, then approved, then renamed (and back to
  pending), then rejected, then resubmitted.
- **SC-002**: Mutation checks fail if the listing filter, the sellable check, or the resubmit-on-edit
  step is removed.
- **SC-003**: A pending or taken-down product is absent from the listing and search, is 404 at its
  address to a shopper, and is priced `sellable = false` - asserted by `ProductReviewTests`.
- **SC-004**: A second approval of the same product is a 409 and produces no second audit entry or
  notice - asserted by `ProductReviewTests`.
- **SC-005**: After the migration, every product that existed before it reads `Approved` (column
  default), with no data step.
- **SC-006**: A customer is refused (403) the review queue and "my decisions" - asserted by Bruno's
  `security-checks/`.

## Assumptions

- One review state per product, not per variant: a shopper sees the product, and one decision covers
  every shape of it.
- The audit log (specs/041) and notifications (specs/042) exist; this feature records and notifies
  through them rather than keeping its own history.
- Moderators (specs/043) and `StaffRoles.Staff` exist; deciding is Admin or Moderator.
- The shop applications queue (specs/044) exists; the dashboard counts it but does not change it.
- An administrator listing a product is the shop listing it (specs/027) and needs no review.

## Out of scope

- Reviewing what an administrator lists.
- Reviewing price or stock changes (decided with the user).
- A history of every decision on one product beyond what the audit log already shows.
- Automatic checks (banned words, image recognition) before a person looks.
- Rejecting one variant while approving the rest.
