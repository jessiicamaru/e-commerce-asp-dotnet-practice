# Research: The Shop Is a Marketplace

> Completed on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

The spec named five decisions. This is what they were settled as, and why the alternatives lost.

On 2026-09-27 each decision was given explicit **Rationale** and **Alternatives considered** headings
around the original wording; nothing was removed.

## D1 - Where the shop name lives, and how Catalog shows it

**Decision**: Identity owns the seller and their shop name. Catalog keeps a **read model** of
`(SellerId, ShopName)`, fed by `SellerRegisteredEvent` and `SellerRenamedEvent` over the broker, and
joins to it locally when it builds a product response.

**Rationale**: This is the pattern the system already uses twice: Catalog's `Availability` is a read model fed by
Inventory's announcements, and an order's shipping address is a frozen copy read once over gRPC. The
rule those follow is that a service answers from its own database.

**Alternatives considered**:

**Rejected: asking Identity over gRPC per product.** FR-003 exists to forbid exactly this. A listing
of 24 products would become 24 lookups, or one batched lookup that still puts Identity on the path of
every anonymous catalogue read — a page that works today with Identity switched off.

**Rejected: freezing the shop name onto the product**, the way an order freezes what it bought. An
order freezes because it is a **record of a past event**; a catalogue listing is a **view of the
present**, and a seller who renames their shop should not have to re-save every product (FR-008).

**The cost, recorded**: the read model is eventually consistent. For a few seconds after somebody
registers or renames, Catalog shows the old name or none. A product whose seller Catalog has not yet
heard of reads as the shop itself, which is the same thing an older product reads as — so the failure
mode is indistinguishable from the normal one, and harmless.

## D2 - How a seller registers, and what the token carries

**Decision**: a separate endpoint, `POST /api/auth/register-seller`, taking everything `register`
takes plus a shop name, and granting the `Seller` role. The token carries `role: Seller`.

**Rationale** - **Why a separate endpoint rather than a flag on `register`**: a boolean that changes what an account
*is* invites the same mistake this project has now fixed twice — `UserId` in a body (specs/009) and a
price in a body (issue #18). A field in a request body that escalates privilege is the shape of a
defect. Two endpoints cannot be confused by accident.

**A seller is also a Customer.** They get both roles, because a seller who cannot buy is a strange
kind of account and every customer-only endpoint would otherwise refuse them.

**Alternatives considered**: a flag on `register` (rejected above), and:

**Rejected: an approval queue** (`Pending → Approved`). It is what a real marketplace does, and it is
roughly twice the work: a state on the seller, an admin screen, a refusal for every write while
pending, and a lifecycle to verify. **The owner was asked and did not answer; this was chosen on
their behalf** and is recorded in the spec's assumptions rather than hidden.

## D3 - What "not yours" returns

**Decision**: **404**, on every write. Not 403.

**Rationale**: The system already does this twice and says why: someone else's address id and someone else's order
id are both 404 precisely so the refusal cannot confirm the thing exists. A 403 on a product a seller
does not own tells them the id is real and somebody has it — which is a catalogue enumeration tool
with extra steps.

**Which operations**: create is a role check (`Seller` or `Admin`). Update, delete, translate, price,
image, add-variant and update-variant are ownership checks. **Admin passes every ownership check**,
because moderation is the job.

**Where the check lives**: in the Application handlers, next to the data, not in a controller
attribute. An attribute cannot see who owns a row, and a check that lives in one of nine places will
be forgotten on the tenth. One helper, called by each handler, with a test per operation (SC-001 says
per operation, not once).

**Alternatives considered**: **403**, rejected above as an enumeration tool; and **a controller
attribute**, rejected above because it runs before the row is read.

*Recorded 2026-09-27:* at the merge `SellerOwnership` is called from **ten** handlers - delete, add
variant, update variant, set and remove translation, set option translation, set and remove variant price,
upload and remove image - where the PR, the class comment and CLAUDE.md say nine. The list in
[contracts/api.md](contracts/api.md) names all ten. Cross-seller tests exist for eight of them; the two
image writes had none at this merge.

## D4 - What an existing product's seller is

**Decision**: `products.SellerId` is **nullable**, and null means **the shop itself**.

**Rationale**: Additive, no backfill, and it means something true: the 14 cameras were listed by the administrator
before sellers existed, and the shop is who sells them. A product an administrator creates from now
on is also the shop's — an administrator moderating a marketplace is not a shop with its own stock,
but the alternative is refusing administrators the ability to list anything, which would break every
existing test and script for no gain.

**Alternatives considered**:

**Rejected: a synthetic "house seller" row.** It would make `SellerId` non-nullable at the cost of
inventing an account that nobody can sign in to, and every join would have to know it is special
anyway.

## D5 - What the storefront shows

**Decision**: the shop name where `Sold by The shop` is hard-coded today, and a way to register as a
seller. **No seller console.**

*Corrected on 2026-09-27:* only the first half shipped. #64 changed the product card and product page to
show `sellerName`; it did not add a seller option to the sign-up page, so registering as a seller was
API-only (`POST /api/auth/register-seller`) at this merge.

**Rationale**: A dashboard for managing one's own listings is the obvious next feature and is a feature, not a
finishing touch: forms for every field a product has, image upload, variant and price editing, in two
languages and two currencies. Bolting a half-version onto this PR would make it unreviewable.
Recorded in the spec as out of scope, and named here so it is a decision rather than an omission.

**Alternatives considered**: a half-built console in this PR - rejected as unreviewable. It became
specs/028.

## D6 - Identity has no broker, and now needs one (found while building)

D1 settled that the shop name crosses as an event. It did not check whether Identity can publish one.
**It cannot**: Identity is the one service in this system with no MassTransit at all - the service map
in CLAUDE.md says so in as many words, and its `.csproj` has no MassTransit package.

So D1's decision costs more than it looked like it did: Identity gains MassTransit, the transactional
outbox, outbox tables and a migration for them, and a dependency on RabbitMQ it did not have.

**Decision**: pay it, and pay it the way the constitution requires - `AddEntityFrameworkOutbox` with
`UseBusOutbox()`, exactly as the other five publishers do. Principle III is non-negotiable: staging
the profile and publishing the event must be one transaction, or a registered seller exists whom
Catalog is never told about.

**Rationale** - **What it does not cost**: Identity does not start needing a broker to work. The publish goes into
the outbox table inside the request's transaction, so registration succeeds with RabbitMQ down and
the delivery service drains the backlog when it returns. The CI job `auth-smoke` runs three services
without a broker, and must keep passing - that is the check on this claim, not the claim itself.

**Alternatives considered** - **Rejected again, for the record**: putting the shop name in the JWT as a claim and having Catalog
read it off any write. It needs no broker at all and is genuinely tempting - but a seller who renames
their shop and never writes another product would keep the old name in the catalogue forever, which
is FR-008 failing silently.

**Verified, not asserted** (from the PR): with RabbitMQ stopped, registration returned 200, the account was
real, the announcement waited in the outbox and sign-in was unaffected; with RabbitMQ back, the outbox
drained to 0 and Catalog received the shop that was registered offline.
