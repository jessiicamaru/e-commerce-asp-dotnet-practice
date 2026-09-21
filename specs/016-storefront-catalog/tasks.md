# Tasks: Browse, Search and Open a Product

- [X] T001 `client/src/api/catalog.ts`: anonymous `listProducts`, `getProduct` and `listCategories`, plus types matching the responses
- [X] T002 `CatalogPage`: listing driven by the URL (search, category, sort, page), plus a pager
- [X] T003 `ProductPage`: detail page, with a 404 message and a note that the price excludes tax
- [X] T004 Routes: `/` goes to the catalogue and `/products/:id` to the product page
- [X] T005 File the missing images as a Catalog issue (#45)
- [ ] T006 PR `Closes #36`; CI green; squash-merge

## What actually happened

Checked through the Vite proxy against the containerised stack:

```text
/api/products?pageSize=2&sortBy=price_desc   -> Expensive Laptop 40,000,000 first, then 120,000
/api/products?pageSize=2&pageNumber=2        -> page 2 / 35, count 69
/api/products?searchTerm=lap                 -> 1: Expensive Laptop
/api/categories                              -> 200
/api/products/<unknown id>                   -> 404
/products/abc (SPA route)                    -> 200 (index.html)
```

`npm run lint` shows no warnings and `npm run build` succeeds. **Not verified in a real browser**: the
pages were type-checked and built, and the API calls they make were checked with curl, but nobody has
clicked through them.
