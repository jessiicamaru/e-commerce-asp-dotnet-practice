# Feature Specification: Shop applications

> Completed on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature Branch**: `044-shop-applications` | **Created**: 2026-09-24 | **Issue**: #89

**Status**: Merged (PR #96, 2026-09-23 21:05 UTC, merge commit `8e5259d`)

**Input**: Issue #89, "opening a shop needs a moderator's approval": "Anybody opens a shop instantly:
`POST /api/auth/register-seller` grants `Seller` in the same request, and the new shop can list products
at once. A marketplace checks who it lets sell." The issue asked for an application (shop name, what they
sell, a contact phone) that stays Pending until a moderator or administrator approves or rejects it with a
reason; approval to grant `Seller`, create the shop and announce it to Catalog through the existing
`SellerRegisteredEvent`; `register-seller` to keep working but create an account plus a pending
application; the applicant to see where their application stands and the moderator console to hold the
queue; notifications and Moderation audit entries for every decision; and today's shops to stay as they
are.

## Why

Anybody could open a shop and start listing products in the same request that created their account. A
marketplace needs to check who it lets sell.

Before this feature `RegisterSellerCommandHandler` gave a brand-new account both `Seller` and `Customer`,
wrote the `seller_profiles` row and published `SellerRegisteredEvent`, all in the request that created the
account. The Seller role is what opens every seller write in Catalog, Inventory and Order (specs/027,
031, 034, 035), so a stranger with an email address could list products the moment they signed up.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Somebody asks to sell (Priority: P1)

A signed-in customer fills in a shop name, what they will sell, and a phone number, and sends an
application. Registering through `register-seller` does the same thing in one step for somebody new: it
creates an account and an application. Until the application is decided, the person is a customer and
nothing more. They can see where their application stands.

**Why this priority**: It is the half of the feature that closes the hole. Once no request can make
somebody a seller on their own say-so, the marketplace is no longer open to anybody - even before staff
have a screen to decide on.

**Independent Test**: Register through `register-seller`, then read the response's roles, the caller's
applications and `/api/sellers/me`. The account holds `Customer` only, one application is `Pending`, and
the seller endpoint answers 403.

**Acceptance**
1. `register-seller` returns roles `["Customer"]`, opens no shop, and tells Catalog nothing.
2. While one application is waiting, a second is refused (409).
3. A person who already sells cannot apply (409).
4. **Given** a signed-in customer with no application, **When** they send a shop name (and optionally a
   description and a phone number), **Then** the answer is 201 with the application `Pending`, and their
   own list shows it.
5. **Given** a request with no token, **When** it tries to apply, **Then** it is refused with 401.
6. **Given** an applicant reads their own applications, **When** the list comes back, **Then** it is
   newest first and carries no applicant name or email - those are for staff.

---

### User Story 2 - Staff decide (Priority: P1)

A moderator or administrator works the queue, oldest first. Approving grants Seller, opens the shop, and
tells Catalog, all at once. Rejecting records a reason, which the applicant reads, and they may apply
again.

**Why this priority**: Equal first with US1, because without it nobody can ever become a seller again:
US1 alone would close the marketplace rather than guard it.

**Independent Test**: As staff, list `Pending`, approve one application, approve it again, and sign in as
the applicant. The first approval is 200, the second 409, the applicant now holds `Seller` and
`Customer`, and the shop's name appears on the applicant's products in the catalogue.

**Acceptance**
1. After approval, the applicant's next sign-in or refresh carries Seller, and the shop name reaches the
   catalogue.
2. Two staff approving at once open one shop. The second approval is 409.
3. A customer or a seller cannot see the queue or decide (403).
4. The applicant is notified of the decision.
5. The decision is recorded in the audit log under Moderation.
6. **Given** a pending application, **When** staff reject it without a reason, **Then** the answer is 400
   and the application is still pending; **When** they reject it with a reason, **Then** it is `Rejected`,
   the reason is stored and shown to the applicant, and the applicant may send a new application.
7. **Given** the queue holds several pending applications, **When** staff read it, **Then** the oldest is
   first, and each carries the applicant's name and email; the approved and rejected lists are newest
   first.

---

### User Story 3 - An approved applicant reaches their shop without signing out (Priority: P2)

The approval happens while the applicant is signed in somewhere with a session issued before it. Their
access token still says `Customer` only, so the seller pages would turn them away. The storefront renews
the session before sending them to the shop.

**Why this priority**: Without it the feature works but looks broken to the one person it just helped:
"Go to my shop" would bounce them until they signed out and in. It is second because the server side is
complete without it - signing in again always works.

**Independent Test**: With an application approved after sign-in, open `/open-shop` and press "Go to my
shop". The storefront renews the session, and `/shop` opens instead of refusing.

**Acceptance**
1. **Given** the latest application is approved and the session predates it, **When** the applicant
   presses "Go to my shop", **Then** the session is renewed first and `/shop` opens.
2. **Given** somebody signed in who does not sell, **When** they open the user menu, **Then** it offers
   "Open a shop"; for a seller it offers the shop instead.
3. **Given** a moderator opens the console, **When** it loads, **Then** it opens on the shop queue.

---

### Edge Cases

- **Two tabs apply at once.** Both pass the "nothing waiting" check before either saves. The database
  admits one pending row per person; the other request gets the same 409 as the check would have given.
- **Five approvals at once.** Exactly one changes the application; the other four get 409 and write
  nothing - no second role row, no second profile, no second event, audit entry or notice.
- **Approve after reject, or reject after approve.** A decided application cannot be decided again;
  either way is 409, naming the state it is already in.
- **An unknown application id.** 404 `Application not found.`
- **Somebody who registered through `register-seller` with an email already in use.** 409 `An account with
  this email already exists.`, as before; an existing customer applies from their account instead.
- **An administrator who holds no `Customer` role.** Refused at the apply endpoint (403): applying is a
  customer's act.
- **A shop from before this feature.** Keeps its role and profile and has no application row; nothing
  reads an application to decide what somebody may do.
- **A rejected applicant applies again.** Allowed; the rejected row stays as history, so the person now
  has two rows.
- **The applicant's session predates the approval.** The role arrives at the next sign-in or refresh
  (US3).

## Requirements

- **FR-001**: At most one pending application per person, enforced by the database.
- **FR-002**: A decision moves an application out of Pending exactly once. Everything the decision does
  commits with it.
- **FR-003**: The queue shows the applicant's name and email to staff only.
- **FR-004**: Shops that exist today keep selling as they are, with no application needed.
- **FR-005**: Registering to sell MUST create a customer account and a pending application, and MUST NOT
  grant the Seller role, create a shop or announce one.
- **FR-006**: A signed-in customer MUST be able to apply with a shop name (required, at most 100
  characters), a description (optional, at most 1000) and a phone number (optional, at most 20). The
  applicant MUST come from the caller's identity, never from the request.
- **FR-007**: A person who already sells, or who has an application waiting, MUST be refused a new
  application.
- **FR-008**: Only a moderator or an administrator MAY read the queue, approve or reject.
- **FR-009**: Approval MUST grant the Seller role, create the shop under the application's name and
  announce it to the catalogue.
- **FR-010**: Rejection MUST carry a reason (at most 500 characters), which the applicant can read. A
  rejected applicant MAY apply again.
- **FR-011**: The applicant MUST be able to read their own applications and where each stands, newest
  first.
- **FR-012**: The applicant MUST be notified of an approval or a rejection, and every decision MUST be
  recorded in the audit log under Moderation. An application MUST be recorded in the audit log too.
- **FR-013**: The queue MUST list pending applications oldest first, so nobody waits behind somebody who
  applied after them.

### Key Entities

- **Shop application**: One person's request to sell under a shop name: what they will sell, a contact
  phone, and where it stands - pending, approved or rejected - with who decided, when, and for a
  rejection, why. A person may have many over time, but only one pending.
- **Shop (seller profile)**: Unchanged by this feature, but now created only by an approval: one per
  account, keyed by the account.

## Success Criteria

- **SC-001**: In one Bruno run, a new seller is refused Seller-only endpoints until approved, and
  approving twice is 409.
- **SC-002**: With five approvals fired at once, exactly one succeeds.
- **SC-003**: 0 shops are opened by registration: after `register-seller` the account holds exactly
  `Customer`, and no shop row and no announcement exist for it.
- **SC-004**: An approval produces exactly one shop, one announcement, one audit entry and one
  notification, and a second approval produces none of them.
- **SC-005**: After a rejection the same person can apply again at once, and their history shows both
  applications.
- **SC-006**: No shop that existed before the feature loses its ability to sell.

## Assumptions

- The Seller role and the shop (`seller_profiles`) from specs/027 stay exactly as they are; this feature
  changes only how somebody gets them.
- Catalog's shop-name read model already consumes `SellerRegisteredEvent` idempotently; the event is sent
  at a different moment, not in a different shape.
- A session learns roles only when it is issued. There is no way to push a new role into a live access
  token, so the applicant's session must be renewed.
- Moderators exist (specs/043) and already have a console to work in.
- Who decides was settled with the user: a moderator or an administrator (see the checklist).

## Out of Scope

- Confirming the applicant's email address before applying or being approved. Added later by specs/063:
  applying needs a confirmed address, and approving a `register-seller` application is 409 until the
  applicant confirms.
- Taking a shop away after approval (closing or suspending a shop). Staff can lock or ban the account
  (specs/043).
- Reviewing what a seller lists. That is specs/045.
- Editing an application after sending it, or withdrawing it.
- A backfill of applications for existing shops (research D3).
