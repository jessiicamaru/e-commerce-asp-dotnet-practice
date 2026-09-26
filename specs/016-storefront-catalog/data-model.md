# Data Model: Browse, Search and Open a Product

> Written on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull request
> and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md)

**No table changed and no migration was added.** The pull request touched no server file.

## Client types (`client/src/api/catalog.ts`)

| Type | Fields |
| :--- | :--- |
| `Product` | `id`, `name`, `description: string \| null`, `price: number`, `availability: 'InStock' \| 'OutOfStock' \| string`, `sku`, `categoryId`, `isActive` |
| `Category` | `id`, `name`, `description \| null`, `slug`, `parentCategoryId \| null`, `isActive` |
| `Page<T>` | `items`, `pageNumber`, `totalPages`, `totalCount`, `hasPreviousPage`, `hasNextPage` |
| `SortBy` | `'name_asc' \| 'name_desc' \| 'price_asc' \| 'price_desc'` |
| `ProductQuery` | `pageNumber?`, `pageSize?`, `categoryId?`, `searchTerm?`, `sortBy?` |

These mirror Catalog's response records at the merge; `availability` is widened to `string` so an
unexpected value shows as "Out of stock" rather than breaking the page.

## URL parameters (the listing's state)

| Parameter | Maps to | Default |
| :--- | :--- | :--- |
| `q` | `searchTerm` | none |
| `category` | `categoryId` | all |
| `sort` | `sortBy` | `name_asc` |
| `page` | `pageNumber` | 1 |

`pageSize` is fixed at 12 (`PAGE_SIZE`), not in the URL.
