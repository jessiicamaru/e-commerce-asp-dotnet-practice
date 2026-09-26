# Contracts: Test runs clean up after themselves

> Written on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**This feature changed no external interface** - no endpoint, message, table or gateway route. It relies on two
existing Catalog endpoints and one existing message:

| Interface | Owner | Used by | Relied on for |
| :-- | :-- | :-- | :-- |
| `DELETE /api/products/{id}` (Admin; specs/024) | Catalog | Bruno `teardown` (through the gateway), both scripts (Catalog directly, `$CATALOG_URL`) | 204 when deleted; 404 when already gone - accepted by the teardown |
| `DELETE /api/categories/{id}` (Admin) | Catalog | the same | 204 when deleted; **409** while a product is still filed under it - hence products first |
| `ProductDeletedEvent` | Catalog → Inventory (`ProductDeletedConsumer`) | nothing in this feature calls it | Inventory dropping the deleted product's stock rows |

The scripts' output lines are informational, not a contract: `cleanup: product <id> -> HTTP <code>` and
`cleanup: category <id> -> HTTP <code>`.
