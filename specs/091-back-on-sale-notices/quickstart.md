# Quickstart: Validating the back-on-sale notices

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d     # Catalog's PostgreSQL on 5433 and SeaweedFS on 8333
```

`DB_PASSWORD`, `SEAWEEDFS_ACCESS_KEY` and `SEAWEEDFS_SECRET_KEY` set (CLAUDE.md, tests).

## Scenario 1 - The server tests (US1-US3, SC-001, SC-002, SC-004)

```bash
cd server
dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SavedProductTests"
```

**Expected**: 11 passed, including `Reactivating_a_variant_in_stock_tells_whoever_saved_it`,
`Approving_a_product_in_stock_that_was_off_the_shelf_tells_whoever_saved_it`,
`Approving_a_product_with_nothing_in_stock_tells_nobody` and `An_edit_that_leaves_it_on_sale_tells_nobody_again`.
Then the whole suite: `dotnet test tests/Ecommerce.Catalog.Tests` - all green.

## Scenario 2 - The words (US4)

```bash
cd client
npx vitest run
```

**Expected**: green. Open the bell in the storefront after Scenario 3: the notice reads "… is available again".

## Scenario 3 - By hand, the approval route

1. As a shopper, save an in-stock product.
2. As its seller, change its description (specs/045 sends it back to review; the saved list shows it unavailable).
3. As a moderator, approve it at `/admin/moderation`.

**Expected**: the shopper's bell has "“…”, which you saved, is available again", and Mailpit (`:8025`) has the email.

## Scenario 4 - Mutations (SC-003)

| Mutation | Expected red |
| :-- | :-- |
| Approval never tells (`if (false)`) | `Approving_a_product_in_stock_that_was_off_the_shelf_tells_whoever_saved_it` |
| Approval ignores stock (`if (to == Approved)`) | `Approving_a_product_with_nothing_in_stock_tells_nobody` |
| `SaveAndRecomputeRollupAsync` runs the callback without a flip | `An_edit_that_leaves_it_on_sale_tells_nobody_again` (and the reactivation count) |
| The callback never runs | `Reactivating_a_variant_in_stock_tells_whoever_saved_it` |
