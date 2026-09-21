# Feature Specification: Browse, Search and Open a Product

**Feature Branch**: `016-storefront-catalog` · **Created**: 2026-09-22 · **Status**: Implemented

**Input**: Issue [#36](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/36), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

The storefront could sign people in but could not show them anything to buy. Browsing needs no
account, so every call here is anonymous.

## Requirements

- **FR-001**: The home page lists products, 12 to a page, with name, category, price and availability.
- **FR-002**: A shopper can search by name or SKU, filter by category, and sort by name or price.
- **FR-003**: Search, filter, sort and page live in the URL, so a result can be shared or reloaded.
- **FR-004**: A product page shows the name, price, availability, description and SKU. An unknown id says the product does not exist.
- **FR-005**: Availability shows only "In stock" / "Out of stock", never a count, because Catalog only has a read model (specs/004).
- **FR-006**: The price is labelled as excluding tax, which is added at checkout for the delivery country (ADR-002).

## What building it found

- **No product has an image.** Nothing in Catalog stores one. Filed as
  [#45](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/45); the page shows a
  placeholder.
- **The listing does not filter out inactive products.** This cannot cause a problem today, because
  nothing sets `IsActive = false` (there is no deactivate command), and checkout refuses an unsellable
  product over gRPC anyway. Recorded here, not filed, because it cannot be reproduced.
- **No backend change was needed.** Paging, search and sorting already existed on `GET /api/products`.
