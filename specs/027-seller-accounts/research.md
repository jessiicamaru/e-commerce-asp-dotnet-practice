# Research: The Shop Is a Marketplace

The spec named five decisions. This is what they were settled as, and why the alternatives lost.

## D1 - Where the shop name lives, and how Catalog shows it

**Decision**: Identity owns the seller and their shop name. Catalog keeps a **read model** of
`(SellerId, ShopName)`, fed by `SellerRegisteredEvent` and `SellerRenamedEvent` over the broker, and
joins to it locally when it builds a product response.

This is the pattern the system already uses twice: Catalog's `Availability` is a read model fed by
Inventory's announcements, and an order's shipping address is a frozen copy read once over gRPC. The
rule those follow is that a service answers from its own database.

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

**Why a separate endpoint rather than a flag on `register`**: a boolean that changes what an account
*is* invites the same mistake this project has now fixed twice — `UserId` in a body (specs/009) and a
price in a body (issue #18). A field in a request body that escalates privilege is the shape of a
defect. Two endpoints cannot be confused by accident.

**A seller is also a Customer.** They get both roles, because a seller who cannot buy is a strange
kind of account and every customer-only endpoint would otherwise refuse them.

**Rejected: an approval queue** (`Pending → Approved`). It is what a real marketplace does, and it is
roughly twice the work: a state on the seller, an admin screen, a refusal for every write while
pending, and a lifecycle to verify. **The owner was asked and did not answer; this was chosen on
their behalf** and is recorded in the spec's assumptions rather than hidden.

## D3 - What "not yours" returns

**Decision**: **404**, on every write. Not 403.

The system already does this twice and says why: someone else's address id and someone else's order
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

## D4 - What an existing product's seller is

**Decision**: `products.SellerId` is **nullable**, and null means **the shop itself**.

Additive, no backfill, and it means something true: the 14 cameras were listed by the administrator
before sellers existed, and the shop is who sells them. A product an administrator creates from now
on is also the shop's — an administrator moderating a marketplace is not a shop with its own stock,
but the alternative is refusing administrators the ability to list anything, which would break every
existing test and script for no gain.

**Rejected: a synthetic "house seller" row.** It would make `SellerId` non-nullable at the cost of
inventing an account that nobody can sign in to, and every join would have to know it is special
anyway.

## D5 - What the storefront shows

**Decision**: the shop name where `Sold by The shop` is hard-coded today, and a way to register as a
seller. **No seller console.**

A dashboard for managing one's own listings is the obvious next feature and is a feature, not a
finishing touch: forms for every field a product has, image upload, variant and price editing, in two
languages and two currencies. Bolting a half-version onto this PR would make it unreviewable.
Recorded in the spec as out of scope, and named here so it is a decision rather than an omission.
