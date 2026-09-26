# Feature Specification: Honest view counts

> Completed on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `086-product-view-limits` | **Created**: 2026-09-27 | **Status**: Merged (#178, 2026-09-26 UTC) | **Issue**: #173 (closes it)

**Input**: Issue #173 - the product view endpoint counted every call, and nothing limited the calls.

## Why

`POST /api/products/{id}/view` is anonymous and always answers 204 (specs/047), and every call added one view.
Nothing limited how often it was called. A reload, or a loop, pushed a product up the administrator's "most
viewed" list and a seller's own views at will.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reload is not a second view (Priority: P1)

A shopper opens a product page, reloads it, comes back to it an hour later. The product has one view from them
today.

**Why this priority**: The honest case is the common one. Every reload inflated the figure the administrator's and
the seller's pages show; without this, "most viewed" measured how often people pressed F5.

**Independent Test**: One visitor id opens the page twenty times at once; the day's count rises by one.

**Acceptance Scenarios**:

1. **Given** a visitor with a stored id, **When** they open the same product twenty times at once, **Then** it
   counts once.
2. **Given** twenty different visitors at once, **When** each opens it, **Then** it counts twenty.
3. **Given** a signed-in shopper, **When** they open it with any visitor id in the body, **Then** they count once -
   their token decides who they are, not the body.
4. **Given** the next shop day, **When** the same visitor opens it again, **Then** it counts again.

---

### User Story 2 - A loop cannot push a product up the list (Priority: P2)

A script calls the view endpoint in a loop, with or without a fresh visitor id each time. After 30 calls in a minute
from one address the gateway refuses the rest with 429; reading products is never affected.

**Why this priority**: The visitor id stops an honest reload, not a script, which can send a new id each time. The
rate limit is what bounds the dishonest case. Second because it only matters for deliberate abuse.

**Independent Test**: With the limit set to 2, a third view call from one client is 429, a call from another client
still passes, and `GET` of the product and its `/saved` route are never limited.

**Acceptance Scenarios**:

1. **Given** one client has made 30 view calls this minute, **When** it makes another, **Then** the gateway answers
   429 with `Retry-After`.
2. **Given** that client is refused, **When** another client makes a view call, **Then** it passes.
3. **Given** that client is refused, **When** it reads products or saves one, **Then** those pass.

---

### User Story 3 - Nothing names a viewer (Priority: P3)

The table that remembers who has already looked holds a hash, never an id, and only today's rows.

**Why this priority**: A list of who looked at what is personal data the shop has no use for beyond today's count.
Third because it constrains how stories 1 and 2 are built rather than adding behaviour.

**Independent Test**: After a view, the stored viewer is 64 hex characters; after the product's first view on the
next day, the earlier day's rows are gone.

**Acceptance Scenarios**:

1. **Given** a view by a signed-in person or a visitor, **When** it is recorded, **Then** `product_viewers.Viewer`
   is a SHA-256 in hex.
2. **Given** viewers recorded yesterday, **When** the product's first view of today is recorded, **Then**
   yesterday's rows for that product are deleted in the same statement.

---

### Edge Cases

- **A call that names nobody** (an older storefront, blocked storage, a direct call): still counts, every time, as
  before. The gateway limit bounds it.
- **Blocked browser storage.** The storefront sends no id rather than a fresh one per call, which would dedupe
  nothing.
- **A stored value that is not a UUID.** Replaced with a new one.
- **Twenty calls at once from one viewer.** One insert wins the primary key; only that one increments the count.
- **A visitor who clears storage, or a script sending a new id each time.** Counts as a new viewer: the known limit
  the rate limit exists for.
- **Behind the storefront's nginx.** Every browser shares nginx's address unless it is a trusted proxy
  (`GATEWAY_TRUSTED_PROXIES`, specs/062); compose trusts it, so the limit is per real client.
- **The limit is per client across all products**, not per product: two different products share one allowance.
- **Views not counted at all** (the product's seller, staff, a product off the shelf): unchanged from specs/047,
  still 204.
- **A product deleted.** Its viewers cascade away with it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** The gateway limits the view route per client IP with its own policy, `views`: 30 calls a minute,
  configurable as `RateLimits:views:*`. It uses the specs/062 machinery and the same trusted-proxy rule. Reading
  products and every other product route stay unlimited.
- **FR-002** A view counts **once a day per viewer**.
  - A signed-in person is identified by their token's id, whatever the body says (Constitution IV).
  - A visitor is identified by a random id the storefront keeps in the browser, sent as `{ "viewer": "<uuid>" }`.
  - A call that names nobody still counts. That covers an older storefront and blocked storage, and the gateway
    limit bounds it.
- **FR-003** It stays a single statement with no read-then-write: the insert into `product_viewers` is the claim,
  and only when it succeeds does `product_views` increment. Twenty calls at once from one viewer count once.
- **FR-004** The endpoint stays anonymous and always 204 (specs/047).
- **FR-005** Nothing names a person:
  - the viewer is stored only as a SHA-256;
  - only today's rows are kept, because the product's first view of a day deletes its earlier rows in the same
    statement.

### Key Entities

- **Product viewer** (`product_viewers`): who has already viewed a product on a shop day, as a hash.
- **Product views** (`product_views`): the count per product per shop day (specs/047, 082), unchanged in shape.
- **Visitor id**: a random UUID the browser keeps in `localStorage` under `visitor-id`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: One visitor twenty times at once counts exactly once; twenty visitors at once count twenty.
- **SC-002**: A signed-in shopper counts once whatever visitor id they send.
- **SC-003**: Over a limit of 2, the third call from one client is 429 while another client passes and product reads
  are never limited.
- **SC-004**: The stored viewer is a 64-hex hash, and earlier days' viewers are gone after the day's first view.
- **SC-005**: Bruno: the same visitor opens the run's product twice and "most viewed" reports exactly one view.
- **SC-006**: Each of five mutations turned its tests red (from the pull request).

## Assumptions

- The gateway sees the real client address, through the trusted-proxy rule of specs/062.
- An honest shopper opens nothing like 30 product pages a minute.
- The storefront is the only honest caller that sends a visitor id.

## Decisions

- **A random id kept by the browser, not the IP.** Behind the storefront's nginx every browser shares one
  address unless the proxy is trusted, and an IP is closer to personal data than a random id is. The id does
  nothing against a script that sends a new id each time; the rate limit is what stops that. The id is there so
  that an honest reload does not count again.
- **Pruning inside the recording statement, not a sweeper.** Catalog runs no hosted services. A data-modifying
  CTE deletes the product's older rows as it records the new one, so the table holds at most a day of viewers per
  product that was viewed.
- **The class keeps its name, `AuthRateLimits`**, although `views` is not about authentication. Renaming it would
  touch the gateway's startup and its tests for no behaviour.

The full reasoning is in [research.md](./research.md).

## Out of scope

- Telling bots from people.
