# Feature Specification: A Cart and an Address Book

**Feature Branch**: `017-storefront-cart` · **Created**: 2026-09-22 · **Status**: Implemented

**Input**: Issue [#37](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/37), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

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

## Decisions

- **The cart is never kept in the browser.** A cart for signed-out visitors would need merging into
  the customer's cart at sign-in. That is a backend feature (Cart has no anonymous cart), so the
  product page asks the visitor to sign in instead.
- **Every change is sent at once and the cart is read again.** Names and prices come from Catalog
  when the cart is read, so the page does not calculate totals itself.

## What building it found

Nothing missing in the backend: every endpoint #37 needs already existed (specs/010, specs/011).
