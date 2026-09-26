# Research: A banned seller's shop is closed

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #193

---

## D1 - A ban closes the shop; a lock does not

**Decision**: Only `BanUserCommand` (and `LiftBanCommand`) announce a suspension.

**Rationale**: A lock is time-boxed (a moderator's ≤ 30 days, an administrator's ≤ 365) and used for behaviour; a ban is
indefinite, administrator-only, and used for what should stop a shop - fraud, counterfeits. A lock that closed the shop
would also need Catalog to reopen it at the lock's end without any event (no message is published when a lock runs
out), i.e. a date on the read model compared with the clock on every read - a second kind of rule for one outcome.

**Alternatives considered**:

- **Any lock or ban closes the shop.** Rejected for the two reasons above; the issue raised it as open.
- **A separate "close shop" moderation action.** Deferred (Out of scope): useful, but the defect is the ban.

---

## D2 - A read model, knowingly

**Decision**: Catalog learns the suspension by message and stores it; nothing asks Identity at read time.

**Rationale**: Specs/031 kept ownership live because a lagging copy refuses the owner - a false refusal indistinguishable
from a real one. A lagging suspension errs the other way: a banned seller's product sells for a few seconds more, an
order staff can cancel (specs/039, refund and restock included). A live call would make every listing, lookup and checkout
depend on Identity, which specs/027 designed away for the shop name.

**Alternatives considered**:

- **Catalog asks Identity over gRPC in pricing only.** Rejected: pricing is not the only path that shows the product -
  the listing would still show it for sale.

---

## D3 - The flag is copied onto each product

**Decision**: `products.SellerSuspended`, written by one `UPDATE ... WHERE "SellerId" = @id` when the suspension is recorded;
`OnShelf` reads it.

**Rationale**: Specs/092 made "on the shelf" one property of the product, read in memory by a dozen call sites and in SQL by
the listing. Keeping the suspension only on `sellers` would make each of them join or load the seller - the scattered rule
#185 just removed.

**Alternatives considered**:

- **A navigation from product to seller.** Rejected: every in-memory check would need the seller loaded, and forgetting to
  include it would read "not suspended".

---

## D4 - Ordering and bans from before

**Decision**: The `sellers` row carries `SuspensionChangedAt`; the record is an upsert guarded on it (older loses). A
missing row is created with the suspension and an empty name the registration fills in. No backfill of existing bans.

**Rationale**: Suspended and reinstated can arrive in either order, and a suspension can overtake the registration; each
read model in this system guards on the announcing side's timestamp (specs/054, 027). Existing bans are known only to
Identity; re-announcing them would need a one-off job, for a state an administrator can re-create by lifting and banning.

**Alternatives considered**: a startup job in Identity republishing every banned seller - rejected as machinery for a case
with a two-click workaround.

---

## D5 - A banned applicant is refused

**Decision**: `ApproveShopApplicationCommand` loads the applicant first and answers 409 when they are banned.

**Rationale**: Otherwise approval would grant `Seller` and announce a registration for a banned person, and Catalog would
see an open shop. Refusing is simpler than announcing a registration and a suspension together.
