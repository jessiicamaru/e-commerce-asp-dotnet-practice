# Research: Administrators manage delivery and the carrier

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #196

---

## D1 - The table is the truth; configuration seeds missing codes

**Decision**: `delivery_options` / `delivery_option_prices` / `carriers` in Order's database. At startup,
`DeliverySeed` inserts each configured option whose code is missing (`INSERT ... ON CONFLICT ("Code") DO NOTHING`, prices only
for rows it inserted) and the carrier if there is none. Nothing stored is updated.

**Rationale**: Specs/011 chose configuration because "two rows that rarely change do not need screens". The management
audit reverses it: staff run the shop from the console and a price is theirs. Seeding keeps every environment (compose,
CI, tests, a fresh database) starting without a manual step. Insert-missing-only is what makes a restart safe: an
administrator's price is never reset by the image's configuration, and a new option added to configuration still appears.

**Alternatives considered**:

- **Configuration overrides the table at startup.** Rejected: every deploy would undo the staff's edits.
- **Seed only an empty table.** Rejected: a code added to configuration later would never appear.
- **A migration inserting the rows.** Rejected: the values are configuration (prices differ per environment), not schema.

---

## D2 - `IShippingOptions` read per request, synchronously

**Decision**: `StoredShippingOptions` (scoped) loads the offered options once per request with a synchronous EF query; the
config reader, `ConfiguredShippingOptions`, is now only the seed's source (still validated at startup).

**Rationale**: The interface is synchronous and used inside FluentValidation rules (`DeliveryOptionRule`); keeping it
avoids rewriting validators as async. Per request means every instance sees an edit at once - a cache would need
invalidation across instances. The table is a handful of rows.

**Alternatives considered**:

- **A singleton cache with a time-to-live.** Rejected: an instance keeps charging the old price until it expires.
- **Make the interface async.** Rejected: churn in validators for no behaviour gained.

---

## D3 - One carrier, and a URL template

**Decision**: A single `carriers` row (id 1, a CHECK constraint): a name and `TrackingUrlTemplate` containing `{reference}`.
`GET /api/orders/delivery/carrier` is anonymous; the storefront's `TrackingLink` fills the reference in (escaped) wherever
the shop carrier's reference is shown - the customer's parcels and order, the seller's and staff's parcel view.

**Rationale**: Decided with the user - the shop has one delivery partner, no courier role. A template turns every stored
reference into a link, including references entered before this feature, with nothing stored per parcel. A public read is
fine: which carrier the shop uses is on every parcel anyway.

**Alternatives considered**:

- **A tracking URL on each parcel.** Rejected: stored per parcel, it would not follow a change of carrier page, and sellers
  would type URLs.
- **Links built on the server into every order response.** Rejected: five response shapes to change for a string the
  storefront can build in one component.
- **Return references (a buyer sending a parcel back).** Left as text: the buyer may post it any way they like.

---

## D4 - The last option on offer stays on

**Decision**: Turning off the last offered option is 409; an offered option must be priced in the default currency (400).

**Rationale**: These are the two startup checks configuration had ("no options", "nothing priced in the shop's own
currency"), moved to the point where an administrator could break them.
