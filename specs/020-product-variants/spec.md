# Feature Specification: Product Variants

**Feature Branch**: `020-product-variants`
**Created**: 2026-09-22
**Status**: Draft
**Input**: The owner's requirement, with the recommended option taken on their behalf throughout.

## Why this exists

A camera is sold as a body alone, or as a kit with a lens, in black or in silver — and each of those
is what is actually priced, stocked, put in a cart and bought. Today a product has **one** price and
**one** SKU, so none of that has anywhere to live. It blocks loading real product data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A product is sold in several shapes (Priority: P1)

An administrator gives a product more than one variant: "Body only" at one price, "Kit with 24-105mm"
at another, each with its own SKU and its own stock.

**Why this priority**: Nothing else in this feature exists without it.

**Independent Test**: Add two variants to a product, then fetch the product. Both are listed, each
with its own price, SKU and option values.

**Acceptance Scenarios**:

1. **Given** a product, **When** an administrator adds a variant with options `Kit = "Body only"` and
   a price and SKU, **Then** the product lists it.
2. **Given** a variant SKU that already exists, **When** it is used again, **Then** it is refused.
3. **Given** a product with two variants at different prices, **When** the listing is read, **Then**
   the product shows the **lowest** price, marked as a "from" price.
4. **Given** a customer or an anonymous caller, **When** they try to add a variant, **Then** they are
   refused and nothing changes.

---

### User Story 2 - A customer buys one particular variant (Priority: P1)

A shopper chooses "Kit with 24-105mm, Black" on the product page, adds **that** to the cart, and is
charged that variant's price. The order says which variant it was, in words, for good.

**Why this priority**: The whole point. A variant that cannot be bought is a catalogue entry, not a
product.

**Independent Test**: Add a variant to the cart, check out, and read the order. The line names the
variant's SKU and its option values, and the total uses that variant's price.

**Acceptance Scenarios**:

1. **Given** a product with variants, **When** a shopper adds one to the cart, **Then** the cart line
   is that variant, and shows its option values and price.
2. **Given** a cart holding a variant, **When** the shopper checks out, **Then** the order line
   freezes the variant id, its SKU, the product name and the option values as text, and the price
   comes from Catalog.
3. **Given** an order placed on a variant, **When** the variant is later renamed, re-priced or
   withdrawn, **Then** the order still describes exactly what was bought.
4. **Given** a variant that is not sellable, **When** a shopper tries to check out with it, **Then**
   the order is refused in full, as an unsellable product is refused today.

---

### User Story 3 - Stock is held against the variant (Priority: P1)

Stock is counted per variant: the black body can be in stock while the silver kit is not.

**Why this priority**: Reserving against the product would oversell one variant and hide another.

**Independent Test**: Set stock on one variant only. The other reads as out of stock, and ordering it
fails at reservation without touching the first variant's stock.

**Acceptance Scenarios**:

1. **Given** two variants of one product, **When** stock is set on one, **Then** only that one reads
   as available.
2. **Given** a variant with 2 on hand, **When** 3 are ordered, **Then** the reservation fails and the
   order fails, with no stock moved.
3. **Given** a successful order, **When** it completes, **Then** exactly that variant's stock is
   deducted.
4. **Given** a product, **When** any of its variants is in stock, **Then** the product reads as in
   stock; when none is, it reads as out of stock.

---

### User Story 4 - Nothing that exists today breaks (Priority: P1)

Every product that exists keeps working: the same price, the same SKU, the same stock, the same
orders, and an earlier image of any service still runs against the new schema.

**Why this priority**: The constitution requires it, and a shop that loses its catalogue on an upgrade
has lost more than the feature is worth.

**Independent Test**: Against a database with products, stock, carts and orders, upgrade and then
place an order as before, naming no variant. It works, and the amounts are unchanged.

**Acceptance Scenarios**:

1. **Given** existing products, **When** the migration runs, **Then** each has exactly one variant
   carrying its current price and SKU.
2. **Given** an existing cart line, an existing reservation and an existing order line, **Then** each
   still resolves to the right variant with no data rewritten.
3. **Given** an earlier image of Order, Cart or Inventory, **When** it runs against the new schema,
   **Then** it still works.

### Edge Cases

- **A product with no variants**: cannot happen through the API (creating a product creates its first
  variant), and a product that somehow has none is not sellable and shows no price.
- **Two variants with the same options**: refused — the options are what a customer picks between.
- **An option value's case**: "Black" and "black" are two different values. **Recorded, not solved.**
- **Deleting a variant**: not offered. A variant is deactivated, because orders refer to it.
- **A cart holding a variant that stops being sellable**: the line stays and is marked, exactly as a
  withdrawn product's line is today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A product has one or more variants. A variant is the unit that is priced, stocked,
  added to a cart and bought.
- **FR-002**: A variant carries its own SKU (unique across the catalogue), its own price, and a set of
  option name/value pairs. The product carries the name, description, category and images.
- **FR-003**: Only an administrator can add or change a variant. No price ever comes from a client.
- **FR-004**: The product listing shows the lowest price of a product's sellable variants, and says
  when variants differ in price.
- **FR-005**: The product lookup includes every variant, with its options, price, SKU and
  availability.
- **FR-006**: A cart line names a variant. Checkout prices that variant and freezes its id, SKU,
  product name and option values onto the order line.
- **FR-007**: Stock and reservations are held per variant.
- **FR-008**: A product reads as in stock when any of its variants is, and out of stock when none is.
- **FR-009**: Every existing product gains exactly one variant, carrying its current price and SKU.
  No existing row is rewritten, and every schema change is additive.
- **FR-010**: An earlier released image of Catalog, Cart, Order or Inventory still runs against the
  new schema and the new messages.
- **FR-011**: A variant that is not sellable cannot be bought, and refuses the order in full.
- **FR-012**: Every new endpoint has Bruno requests, including the refusals.

### Key Entities

- **Product**: what a shopper recognises — name, description, category, images. No price of its own
  any more, beyond the "from" price derived from its variants.
- **Variant**: a sellable shape of a product — SKU, price, options, availability.
- **Option**: a name and a value on a variant (`Kit` / `Body only`). Free text, by design: a camera
  has kits, a shirt has sizes, and the schema should not need to know which.

## Success Criteria *(mandatory)*

- **SC-001**: A product can be sold in several priced shapes, and a customer can buy exactly one of
  them.
- **SC-002**: An order placed on a variant still describes what was bought after that variant is
  renamed, re-priced or withdrawn.
- **SC-003**: Selling one variant never moves another variant's stock.
- **SC-004**: After the upgrade, every product that existed still sells at the same price, and the
  end-to-end saga check passes unchanged.

## Assumptions

- One currency for now. A second currency is the next feature, and its price list hangs off the
  variant, not the product.
- No variant-level images. The product's image is shown for every variant.
- No option ordering or option "types" (a colour swatch versus a dropdown): the storefront renders
  whatever options a variant carries.
