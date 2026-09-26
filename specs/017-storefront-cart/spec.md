# Feature Specification: A Cart and an Address Book

> Completed on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature Branch**: `017-storefront-cart` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#47](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/47), 2026-09-22 (03:03, UTC+7)

**Input**: Issue [#37](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/37), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

A customer could find a product (specs/016) but not keep it, and had nowhere to say where an order
should go. Checkout (specs/018) needs both a cart and an address before it has anything to price.

## User Scenarios & Testing

### User Story 1 - A customer puts products in a cart that follows them (Priority: P1)

**Why this priority**: without a cart there is nothing to check out.

**Independent Test**: signed in, add a product twice from its page; sign out and back in; the cart
still holds it.

**Acceptance Scenarios**:

1. **Given** a signed-in customer on a product page, **When** they choose a quantity and add it,
   **Then** the page says "Added N to your cart." with a link to the cart.
2. **Given** a signed-out visitor on a product page, **When** they look for the button, **Then** they
   are offered "Sign in to add this to your cart", and signing in brings them back to that product.
3. **Given** a cart with lines, **When** the customer signs out and back in, **Then** the lines are
   still there.

### User Story 2 - A customer changes the cart and sees what it would cost (Priority: P1)

**Why this priority**: equal first; a cart that cannot be corrected is not usable.

**Independent Test**: on `/cart`, change a quantity, remove a line; the totals come back from the
server each time.

**Acceptance Scenarios**:

1. **Given** a cart, **When** it is opened, **Then** each line shows its current name, price and line
   total, and the page shows an **estimated** total "before shipping and tax".
2. **Given** a line, **When** its quantity is changed or it is removed, **Then** the change is sent at
   once and the cart is read again.
3. **Given** a line that cannot be bought, **When** the cart is shown, **Then** the line says why
   (no longer for sale, removed from the shop, price unavailable) and the page says to fix it before
   checking out.
4. **Given** Catalog unreachable, **When** the cart is opened, **Then** the page says prices could not
   be checked and keeps the lines.
5. **Given** a quantity the server refuses (adding 0 or less, or setting a negative one), **When** it
   answers 400, **Then** the page shows the server's message. (Setting a line to 0 removes it - Cart's
   own rule.)

### User Story 3 - A customer keeps an address book (Priority: P2)

**Why this priority**: second - needed before checkout, not before shopping.

**Independent Test**: on `/addresses`, add two addresses, make the second the default, edit it, delete
the first.

**Acceptance Scenarios**:

1. **Given** no addresses, **When** the first is saved, **Then** it becomes the default.
2. **Given** two addresses, **When** the second is made default, **Then** only it is marked default.
3. **Given** an invalid postal code and country, **When** saved, **Then** Identity's messages appear
   next to those two fields.
4. **Given** an address, **When** it is edited or deleted, **Then** the list reflects it.

### Edge Cases

- **A cart for signed-out visitors** would need merging into the customer's cart at sign-in; Cart has
  no anonymous cart, so the page asks the visitor to sign in instead (Decisions).
- **A price changes between adding and viewing**: the cart shows today's price, because it stores none.
- **Adding the same product again** adds to the line's quantity (Cart's behaviour, specs/010).
- **Editing an address after ordering** changes nothing on past orders - Order copies the address at
  checkout (specs/011).

## Requirements

- **FR-001**: A signed-in customer can add a product to the cart from its page, choosing a quantity. A
  signed-out visitor is offered sign-in instead, and comes back to the product afterwards.
- **FR-002**: The cart page lists each line with its current name, price and line total, and lets
  the customer change the quantity or remove the line.
- **FR-003**: The cart shows its total as an **estimate**, before shipping and tax. The amount charged
  is fixed at checkout (specs/010, ADR-002).
- **FR-004**: A line that cannot be bought says why, using Cart's own `status`. When Catalog is
  unreachable the page says so and keeps the lines.
- **FR-005**: The cart is kept by the Cart service, per customer, so it survives signing out and
  back in.
- **FR-006**: A customer can add, edit and delete delivery addresses and mark one as the default.
  Identity's validation messages appear next to the field they belong to.
- **FR-007**: The cart and address pages require a signed-in customer and wait for the session to be
  restored before deciding (specs/015).

### Key Entities

- **Cart (as shown)** - lines, an estimated total or none, whether it can be checked out, whether
  prices were available.
- **Cart line (as shown)** - product id, name, quantity, unit price, line total, status.
- **Address** - recipient, two lines, city, region, postal code, two-letter country, phone; whether it
  is the default.

## Success Criteria

- **SC-001**: A cart survives sign-out and sign-in in 100% of cases.
- **SC-002**: No total on the cart page is computed by the client; every figure is the server's.
- **SC-003**: After any "make default", exactly one address is the default.
- **SC-004**: Every field error Identity returns appears beside its field.

## Decisions

- **The cart is never kept in the browser.** A cart for signed-out visitors would need merging into
  the customer's cart at sign-in. That is a backend feature (Cart has no anonymous cart), so the
  product page asks the visitor to sign in instead.
- **Every change is sent at once and the cart is read again.** Names and prices come from Catalog
  when the cart is read, so the page does not calculate totals itself.

## What building it found

Nothing missing in the backend: every endpoint #37 needs already existed (specs/010, specs/011).

## Assumptions

- Cart (specs/010) and the address book in Identity (specs/011) already provide every endpoint.
- The country is a two-letter ISO code and decides the tax rate at checkout (specs/012).

## Out of Scope

- An anonymous cart and merging it at sign-in.
- Checkout itself (specs/018).
