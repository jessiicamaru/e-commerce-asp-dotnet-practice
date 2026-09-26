# HTTP Contract: Browse, Search and Open a Product

> Written on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull request
> and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature**: [spec.md](../spec.md)

**No API changed.** The pages rely on three Catalog endpoints that already existed, all
`[AllowAnonymous]`, reached through the gateway at `http://localhost:5000`. Recorded as they were at the
merge, so a later change to them can be checked against what the storefront assumes.

---

## `GET /api/products` - anonymous

Query (bound to `GetProductsQuery`): `pageNumber` (default 1), `pageSize` (default 10 - the storefront
sends 12), `categoryId`, `searchTerm`, `sortBy`.

- `searchTerm` matches name **or** SKU, case-insensitive, as a substring.
- `sortBy`: `price_asc`, `price_desc`, `name_desc`; anything else, or nothing, is name ascending.

`200`:

```json
{
  "items": [
    { "id": "...", "name": "Expensive Laptop", "description": "...", "price": 40000000,
      "availability": "InStock | OutOfStock", "sku": "...", "categoryId": "...", "isActive": true }
  ],
  "pageNumber": 1, "totalPages": 35, "totalCount": 69,
  "hasPreviousPage": false, "hasNextPage": true
}
```

(The numbers are from the pull request's run with `pageSize=2`.) No stock count - by design since
specs/004. Inactive products are not filtered out.

## `GET /api/products/{id}` - anonymous

`200` with one product in the shape above. `404` when the id is unknown - with a body of
`{ "message": "Product not found." }` returned by the controller itself, **not** ProblemDetails. The
product page only reads the status, so the shape does not matter to it.

## `GET /api/categories` - anonymous

`200` with an array (not paged) of `{ "id", "name", "description", "slug", "parentCategoryId",
"isActive" }`.

---

## Storefront routes

| Route | Served by | Notes |
| :--- | :--- | :--- |
| `/` | `CatalogPage` | query `q`, `category`, `sort`, `page` |
| `/products/:id` | `ProductPage` | a deep link answers 200 with `index.html` (the SPA); verified for `/products/abc` |
