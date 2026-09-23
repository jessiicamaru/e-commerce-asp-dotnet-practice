# Feature Specification: Which shop each parcel comes from

**Feature branch**: `036-parcel-shop-names`
**Created**: 2026-09-23
**Status**: Draft

## What is wrong

Since specs/035 an order that holds goods from two sellers shows the customer two parcels - "Parcel 1
of 2", "Parcel 2 of 2" - with what is in each. It never says **who is sending them**. Two boxes
arrive from two senders the customer has never heard named; the question "which shop is this from"
has no answer on the page, and nor does "who do I ask about the late one".

Order cannot say, because it holds no shop names: it froze who sold each line (specs/034), not what
they are called.

## User Scenarios

### US1 - The customer sees who sends each parcel (P1)

**Acceptance**
1. Each parcel on a multi-parcel order is headed with its shop's name, or "the shop" for the shop's
   own goods.
2. Each line of an order says which shop sold it, so a one-parcel order from a seller says so too.
3. The checkout summary says which shop sells each line, before the customer pays.

### US2 - The name is the one at the time of purchase (P1)

**Acceptance**
1. A shop renamed after an order was placed does not rename that order - the order is a record of
   who the customer bought from, like the price and the product's name.

### Edge cases

- **Orders from before this** have no names recorded. Their parcels read as they do now.
- **A shop whose name Catalog has not heard yet** (its registration event is still in the broker):
  the line records no name, and the order reads as it would have before - never a blank or an id.
- **A Catalog older than this** sends no name: the same.

## Requirements

- **FR-001** At checkout each line MUST record the name of the shop that sells it, as the catalogue
  knows it at that moment, and keep it.
- **FR-002** The shop's own goods record no shop name; they are shown as the shop.
- **FR-003** An unknown name MUST be recorded as none, never as an empty or placeholder string.
- **FR-004** The order's lines, its parcels and the checkout summary MUST carry the name.
- **FR-005** Nothing about a seller other than the name is disclosed to the customer.

## Out of scope

- Contacting a seller, or a seller's profile page.
- Backfilling names onto older orders (Order cannot read Catalog's database, and asking now would give
  today's name - specs/034 research D7, same reason).

## Success Criteria

- **SC-001** A two-seller order names both shops on its parcels.
- **SC-002** Renaming a shop afterwards leaves the order's names unchanged.
- **SC-003** `verify-saga.sh` passes; an order of the shop's own goods reads as before.
