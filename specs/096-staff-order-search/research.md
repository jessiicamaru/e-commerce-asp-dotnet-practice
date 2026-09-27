# Research: Staff find any order

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #194

---

## D1 - An email is resolved by the storefront, through Identity

**Decision**: `GET /api/orders/staff` takes a `customerId`; the storefront turns an email into that id with Identity's staff
search (`GET /api/users?search=`, specs/043) and labels each row with `GET /api/users/lookup` (specs/047).

**Rationale**: Order stores user ids and never emails (Principle I; the address it freezes is a delivery address, not the
account's). The administrator overview already composes Order's buyer ids with Identity's lookup. A person is found only
when the email matches exactly (case-insensitively); anything else is "nobody has that address", and Order is not asked.

**Alternatives considered**:

- **Order calls Identity over gRPC to resolve an email.** Rejected: a new synchronous edge, and a new permission on
  Identity's gRPC service, for one staff screen.
- **Copy the customer's email onto the order.** Rejected: an email changes (#104 allows nothing yet, but will) and an
  order is a record of a purchase, not of an account.

---

## D2 - Administrators only

**Decision**: `[Authorize(Roles = "Admin")]`, like every other owner-unscoped order read.

**Rationale**: The rows name every customer's orders and amounts; the staff order view it links to shows addresses.
Specs/038 made that read Admin-only and warned that its handler must sit behind no other route; this is the same kind.

**Alternatives considered**: open to moderators for customer service - rejected here, out of scope in the spec.

---

## D3 - The id prefix is matched on the text of the id

**Decision**: `EF.Functions.Like(x.Id.ToString(), prefix + "%")`, the prefix lower-cased, 4-36 of `[0-9a-f-]`.

**Rationale**: Npgsql translates `Guid.ToString()` to `"Id"::text`, which PostgreSQL writes lower-case and hyphenated -
exactly what the storefront and emails show. Four characters is the shortest prefix that is a search rather than a scan
of a sixteenth of the table. No index: `LIKE 'x%'` on a cast is a scan, acceptable for a staff screen at this scale; an
expression index can follow if the table grows.

**Alternatives considered**:

- **Parse a full Guid only.** Rejected: customers quote the first characters.
- **A trigram index as in specs/074.** Rejected as premature for a staff-only query.

---

## D4 - Newest first, and a response of its own

**Decision**: `StaffOrderSummaryResponse` - the customer list's row plus `UserId` - ordered by `CreatedAt` descending.

**Rationale**: The fulfilment queue is oldest first because it is work; a search is lookup, where the recent order is the
one asked about. The customer's own `OrderSummaryResponse` is not widened with a user id: shapes that customers receive
stay as they are.
