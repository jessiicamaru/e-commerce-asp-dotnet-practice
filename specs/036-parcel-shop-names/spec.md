# Feature Specification: Which shop each parcel comes from

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature branch**: `036-parcel-shop-names`
**Created**: 2026-09-23
**Status**: Merged (#80, 2026-09-23)
**Issue**: none - the pull request closes no issue.

## What is wrong

Since specs/035 an order that holds goods from two sellers shows the customer two parcels - "Parcel 1
of 2", "Parcel 2 of 2" - with what is in each. It never says **who is sending them**. Two boxes
arrive from two senders the customer has never heard named; the question "which shop is this from"
has no answer on the page, and nor does "who do I ask about the late one".

Order cannot say, because it holds no shop names: it froze who sold each line (specs/034), not what
they are called.

## User Scenarios & Testing

### US1 - The customer sees who sends each parcel (Priority: P1)

**Why this priority**: it is the whole gap - two unnamed boxes from two unnamed senders.

**Independent Test**: place an order holding two sellers' goods and one of the shop's; the quote, the
lines and the parcels each name the right shop, and the shop's own parcel reads "the shop".

**Acceptance Scenarios**:

1. **Given** a multi-parcel order, **When** the customer opens it, **Then** each parcel is headed with its
   shop's name, or "the shop" for the shop's own goods.
2. **Given** any order, **When** its lines are shown, **Then** each line says which shop sold it, so a
   one-parcel order from a seller says so too.
3. **Given** a checkout in progress, **When** the summary is shown, **Then** it says which shop sells each
   line, before the customer pays.

---

### US2 - The name is the one at the time of purchase (Priority: P1)

**Why this priority**: a name that changes under a past order makes the order say the customer bought
from somebody they did not; it is what decides frozen over looked-up (research D1).

**Independent Test**: place an order, rename the seller's shop, wait until the catalogue shows the new
name, and re-read the order: it still shows the old one.

**Acceptance Scenarios**:

1. **Given** an order already placed, **When** its shop is renamed afterwards, **Then** that order is not
   renamed - the order is a record of who the customer bought from, like the price and the product's
   name.

---

### Edge cases

- **Orders from before this** have no names recorded. Their parcels read as they do now.
- **A shop whose name Catalog has not heard yet** (its registration event is still in the broker):
  the line records no name, and the order reads as it would have before - never a blank or an id.
- **A Catalog older than this** sends no name: the same.
- **An empty string on the wire** is treated as no name (`GrpcCatalogPrices.SellerNameOf`), so it never
  prints as a blank.

## Requirements

### Functional Requirements

- **FR-001** At checkout each line MUST record the name of the shop that sells it, as the catalogue
  knows it at that moment, and keep it.
- **FR-002** The shop's own goods record no shop name; they are shown as the shop.
- **FR-003** An unknown name MUST be recorded as none, never as an empty or placeholder string.
- **FR-004** The order's lines, its parcels and the checkout summary MUST carry the name.
- **FR-005** Nothing about a seller other than the name is disclosed to the customer.

### Key Entities

- **Order line**: gains the selling shop's name, frozen at checkout beside the seller id specs/034
  froze. None for the shop's own goods or when the name was not known.
- **Parcel (shipment part)**: shows the name frozen on its lines, and whether it is the shop's own.

## Out of scope

- Contacting a seller, or a seller's profile page.
- Backfilling names onto older orders (Order cannot read Catalog's database, and asking now would give
  today's name - specs/034 research D7, same reason).

## Success Criteria

- **SC-001** A two-seller order names both shops on its parcels.
- **SC-002** Renaming a shop afterwards leaves the order's names unchanged.
- **SC-003** `verify-saga.sh` passes; an order of the shop's own goods reads as before.

## Assumptions

*(Added in this backfill.)*

- Catalog's `sellers` read model (specs/027) is the source of a shop's name, fed by
  `SellerRegisteredEvent` / `SellerRenamedEvent`; it can lag a new seller by the time a message takes.
- Every line of one parcel belongs to the same seller (specs/035), so any recorded name on those lines
  names the parcel.
