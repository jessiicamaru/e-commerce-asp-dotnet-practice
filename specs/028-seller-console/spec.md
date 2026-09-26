# Feature Specification: A seller can actually sell

> Completed on 2026-09-27, after the feature merged (#65), from the code at that merge, the pull request, docs/features/marketplace.md and docs/architecture/storefront.md.

**Feature branch**: `028-seller-console`
**Created**: 2026-09-23
**Status**: Merged as [#65](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/65) on 2026-09-22 (written as Draft; corrected on 2026-09-27)
**Input**: specs/027 gave the shop real sellers and gave them endpoints. It gave them no page.
A seller can be created, can hold the role, can own products - and has nowhere to click. The
request behind 027 was "seller co quyen duoc dang tai cac san pham minh ban"; the right exists
and the act does not.

## Why this is a feature and not a follow-up

specs/027 research D5 called a seller console out of scope, and that was the right call for that
change: the authorization model had to be correct before anything drove it. It is correct now, and
proven - nine write handlers behind one ownership check, a 404 for somebody else's listing, an
administrator passing every one of them. What remains is that the only way to exercise any of it is
a shell script with a bearer token.

**The storefront is this project's honesty check.** CLAUDE.md says its purpose is to exercise the
API as a person would and to file what the backend lacks. Nothing has ever driven the write side of
Catalog through a browser. Every product in the database was posted by a Python seeder run by an
administrator.

## User Scenarios

### US1 - A seller sees their own listings (P1)

Alice signs in. Because she holds `Seller`, the storefront offers her a shop. It lists the products
that are hers, each with its name in the language she is reading, its price in the currency she has
chosen, and whether it is in stock.

**Why this priority**: Without a page that shows a seller her own listings, nothing else in this
feature has anywhere to live.

**Independently testable**: sign in as a seller with listings, see exactly those listings. Sign in
as a customer, see no shop at all.

**Acceptance**
1. A seller with listings sees them, and sees no listing belonging to another seller.
2. A seller with no listings sees an empty state that says what to do, not a blank page.
3. A customer sees no route to a shop, and typing the address lands them somewhere sensible.
4. The page asks Catalog once, with the caller's token, and takes no seller id from the address.

### US2 - A seller lists a product (P1)

Alice names a product, picks a category, describes it, gives a SKU and a price, and it appears in
the catalogue where a shopper can find it.

**Why this priority**: It is the act specs/027 granted the right to - "dang tai cac san pham minh ban".

**Independently testable**: create a product through the page, then find it on the public catalogue
page as an anonymous visitor.

**Acceptance**
1. A product created through the page is owned by the seller who created it - it appears on their
   shop and on the public catalogue with their shop name under it.
2. A price the currency cannot hold is refused with the reason shown, not swallowed.
3. A duplicate SKU is refused with the reason shown.
4. Creating a product does not require touching any other page to make it visible.

### US3 - A seller corrects and withdraws (P2)

Alice fixes a price, uploads a photograph, and takes a listing down.

**Why this priority**: A listing that cannot be corrected is a liability, but it is only reachable once
US2 has created one.

**Independently testable**: open one of the seller's listings, set a dollar price, upload an image,
withdraw it; each change shows on the public catalogue.

**Acceptance**
1. Changing a price changes what the catalogue shows and what checkout charges.
2. An image appears on the public product page.
3. A withdrawn product leaves the catalogue, and the stock rows go with it.
4. Every refusal from the server is shown in words - a failed write never looks like a slow one.

### US4 - A seller renames their shop (P3)

**Why this priority**: The name already shows under every listing (specs/027); changing it is a
convenience.

**Independently testable**: rename the shop from `/shop` and read any of the seller's listings on the
public catalogue.

**Acceptance**
1. The new name appears under every one of their listings.
2. No product row is written.

## Edge Cases

- **A customer types `/shop`.** `RequireRole` sends a signed-in customer to `/` and a signed-out visitor
  to sign in; it decides nothing while the session is still being restored.
- **A reload.** The refresh response carries the same roles as the sign-in, so pressing F5 does not take
  a seller's shop away (`Refreshing_a_session_keeps_the_roles`).
- **Somebody else's listing at `/shop/products/:id`.** The server answers 404, and the page shows it as
  "not found", never as "not allowed" - translating it would undo the reason it is a 404.
- **A seller browsing in dollars creates a product.** The price field is the **default** currency's (VND),
  whatever the seller is reading in (research D2).
- **A price in a currency the seller is not browsing in.** The editor reads the product once per currency
  so an existing VND price does not show as an empty box; a currency with no price says so rather than
  showing 0.
- **Withdrawing.** Asks for confirmation first.
- **A new listing's stock.** Zero, and the form says so (research D5).
- **A price the currency cannot hold, a duplicate SKU.** The server's words, shown as they are.

## Key Entities

- **Roles on the authentication response**: what the signed-in person holds, for drawing only.
- **A seller's listing**: a product whose `SellerId` is the caller (specs/027), read through
  `/api/products/mine`.
- **Shop**: the seller's shop name (`/api/sellers/me`).

## Requirements

- **FR-001** The storefront MUST know the signed-in person's roles, and MUST learn them from the
  server rather than inferring them.
- **FR-002** The seller pages MUST be reachable only by a signed-in seller, and this MUST be a
  convenience rather than a control: every refusal that matters is the server's.
- **FR-003** The shop page MUST read the seller's own listings through the endpoint that takes no
  seller id.
- **FR-004** Creating a product MUST work in one submission - name, category, description, SKU and
  a price in the active currency.
- **FR-005** Every server refusal MUST surface its message. The `ValidationException`,
  `ConflictException` and `NotFoundException` details are already sent outside Development
  (specs/022); the storefront MUST show them.
- **FR-006** Every string MUST be translated in both languages, the storefront's own rule.
- **FR-007** A seller's own products page MUST show the product in the language and currency the
  seller is reading, like every other page.

## Out of scope

- **An administrator console.** Admin holds every one of these rights already and passes every
  ownership check; a page for it is a second feature with a different threat model.
- **Variants beyond the first.** Creating a product creates its first variant (specs/020). Adding a
  second variant through the browser is a form with a different shape and waits.
- **Stock.** Inventory is a separate service and a seller setting their own stock is a decision
  nobody has made yet. Recorded as the first thing this page will be asked for.
- **Seller approval.** Chosen in specs/027: a seller is usable immediately.

## Success Criteria

- **SC-001** A person can register as a seller, list a product and see it on the public catalogue
  without a terminal.
- **SC-002** A seller cannot see or change another seller's listing through the storefront, and the
  server refuses it independently of what the storefront offers.
- **SC-003** Both languages and both currencies work on every new page.
- **SC-004** (added 2026-09-27) The storefront has automated tests that fail when a guard lets the wrong
  person through, a form sends the wrong thing, or a refusal is not shown - and CI runs them.

Measured at the merge (from the PR): SC-001 by screenshots of every new page; SC-002 against the API with
two real sellers - Bob was refused all six writes on Alice's listing with **404 every time, 403 never**,
and `alice sees 1, bob sees 0`; SC-003 by screenshots in both languages plus English-and-dollars; SC-004 by
33 client tests in the CI `client` job.

## Assumptions

- A seller is also a customer (specs/027 grants both), so the seller pages live inside the same
  layout and the same session. There is no separate sign-in.
- The first variant reuses the product id (specs/020), so a one-form create is enough to produce a
  sellable product.
