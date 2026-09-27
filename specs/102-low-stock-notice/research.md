# Research: A seller is told when a variant runs low

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #200

---

## D1 - Inventory announces the crossing and Catalog tells the seller

**Decision**: Inventory publishes `StockRanLowEvent(VariantId, QuantityAvailable, Threshold, OccurredAt)` through its
outbox in the reservation transaction. Catalog consumes it, looks up the variant's product, seller and option summary,
and calls `INotifier`.

**Rationale**: The issue asks whether the notice should make a synchronous call to Catalog (as specs/031 does for
stock permissions) or whether the seller id should travel on the stock row.

- A synchronous call in the reservation path puts Catalog's latency and availability inside checkout. It could not sit
  inside the `FOR UPDATE` transaction (specs/031's rule), and outside it the notice and the stock change would no
  longer commit together.
- A seller id on the stock row needs a new field on `ProductCreatedEvent` and `ProductVariantCreatedEvent`, plus a
  backfill across two databases, and it goes stale when ownership changes.
- The notice also needs words Inventory does not have, namely the product's name and the variant's options.

Catalog has all of that, including whether the product is the shop's own. A notice is display, where eventual
consistency is this project's accepted answer (specs/031: "about display, where stale costs a slightly old page").

**Alternatives considered**: a live gRPC call to Catalog, and a seller id on `stock_items`. Both were rejected for the
reasons above.

---

## D2 - The crossing is judged from before and after in one locked transaction

**Decision**: `LowStock.Crossed(before, after, threshold)` is true when `threshold > 0 && before >= threshold && after
< threshold`. The reservation handler records each locked row's `QuantityAvailable` before it adds to
`QuantityReserved`, and evaluates the rule after.

**Rationale**: The rows are held `FOR UPDATE`, so the before value is exact and no concurrent sale can interleave. Two
simultaneous checkouts serialise, and only one of them crosses. No stored flag is needed. A flag would have to be kept
right by every path that raises stock (release, expiry, restock, returns, set-on-hand), which is seven places to
forget. Being stateless makes "once per crossing" fall out of the arithmetic: a return above the line makes the next
fall a new crossing, and staying below never crosses. Redelivery is covered by the reservation's existing idempotency:
a second delivery for an order changes nothing, so it publishes nothing.

**Alternatives considered**: a `LowSince` column cleared by every raising path. Rejected for the seven-places reason.
It is the same argument as specs/075's "the rollup's own flip".

---

## D3 - A sale is the reservation

**Decision**: Only `ReserveStockCommandHandler` publishes. Confirmation (`OrderCompletedEvent`) moves on-hand and
reserved together, which leaves available unchanged, so it cannot cross.

**Rationale**: Available stock is what a shopper can still buy, so it is what a seller restocks against. It drops at
reservation. A payment that then fails returns the units. The seller was told a little early and is told again at the
next real crossing, which is the safer mistake than being told late. Waiting for payment would mean a before/after on
on-hand, which ignores units held by other checkouts and so could say "5 left" when 2 are sellable.

---

## D4 - The threshold: shop default plus a per-variant override

**Decision**: `stock_items.LowStockThreshold` is nullable, and null means the default. The default is
`Inventory:LowStock:DefaultThreshold`, 5, validated at startup to be between 0 and 100,000. 0 turns the notice off. The
threshold is set by `PUT /api/stock/{variantId}/low-stock-threshold` with `StockOwnership`'s check, the same one as
setting stock.

**Rationale**: A default in configuration needs no screen and no migration, and the few variants that differ carry their
own number. Keeping null distinct from 5 means a later change of the default reaches every variant that never chose.
A separate endpoint keeps `PUT /api/stock/{id}`'s body, the stock count, unchanged, so no client breaks.

**Alternatives considered**: a column with a database default of 5, rejected because changing the default would then
need a data migration; and the threshold in the stock `PUT` body, rejected as a contract change for every caller.

---

## D5 - Nobody is told about the shop's own products

**Decision**: When the product has no seller, Catalog sends no notice. The issue's "unless staff opt in" is not built.

**Rationale**: A notice goes to one person (specs/042). "The shop" is a role, not a person. Telling every administrator
and moderator would be noise for the moderators, and choosing one administrator is a setting nobody has asked for.
Staff already see stock on the shop's own listings. This is recorded as out of scope rather than half-built.
