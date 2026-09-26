# Tasks: Browse, Search and Open a Product

> Completed on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull
> request and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `016-storefront-catalog`

**Tests**: None automated - the client had no test runner until specs/028. The calls were checked with
curl through the proxy; CI lints and builds.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (browse), US2 (narrow and share), US3 (one product)

T001-T006 are the list written at the merge, kept as they were.

- [X] T001 `client/src/api/catalog.ts`: anonymous `listProducts`, `getProduct` and `listCategories`, plus types matching the responses
- [X] T002 `CatalogPage`: listing driven by the URL (search, category, sort, page), plus a pager
- [X] T003 `ProductPage`: detail page, with a 404 message and a note that the price excludes tax
- [X] T004 Routes: `/` goes to the catalogue and `/products/:id` to the product page
- [X] T005 File the missing images as a Catalog issue (#45)

## Recorded after the merge

Added on 2026-09-27 from the diff of #46.

- [X] T007 [P] [US1] `Availability` in `client/src/pages/CatalogPage.tsx`, exported and reused by `ProductPage.tsx`, so both say "In stock" / "Out of stock" the same way and never a count
- [X] T008 [P] [US1] `money()` in `client/src/api/catalog.ts`: two decimals, formatted and never computed with
- [X] T009 [US2] Reset to page 1 whenever search, category or sort changes (`update()` drops `page`), in `CatalogPage.tsx`
- [X] T010 [US3] Keep each answer with the id it answers in `ProductPage.tsx`, so a stale response for a previous id is ignored during render
- [X] T011 [P] [US1] Placeholder tile and grid styles in `client/src/index.css`
- [X] T012 Record in `specs/016-storefront-catalog/spec.md` that inactive products are not filtered, and why it was not filed
- [X] T006 PR [#46](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/46) `Closes #36`; CI green; squash-merged as `67147f4` on 2026-09-22

## Dependencies & Execution Order

T001 first; T002 and T003 in parallel after it; T004 needs both; T007-T011 belong to the pages they
name. T005 and T012 are independent.

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

## Notes

- **T006 is listed last although its id is lower**: it was the last task at the merge; T007-T012
  describe work already inside that pull request.
- 12 tasks, all done.
