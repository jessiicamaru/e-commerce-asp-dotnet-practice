# Quickstart: Validating cart removal by variant

> Written on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d        # Cart's database on 5439
```

## Scenario 1 - The tests (US1, US2, SC-001 to SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Cart.Tests --filter "FullyQualifiedName~VariantLineTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Cart.Tests
```

**Expected**: green. The four tests this feature added:

| Test | Proves |
| :-- | :-- |
| `A_completed_order_takes_out_the_variant_it_bought_not_its_sibling` | US1 - failed before the fix |
| `An_item_naming_no_variant_takes_out_the_line_whose_variant_is_the_product` | US2 scenario 1 |
| `A_line_from_before_variants_is_matched_by_its_product` | US2 scenario 2 |
| `The_variant_travels_from_the_event_and_an_empty_one_falls_back_to_the_product` | FR-001, `OrderedItem.From` |

The whole project: 18 tests at the merge (14 before, plus these four).

## Scenario 2 - Mutation checks (evidence the tests bite)

Each change to `CheckoutOutcomes.cs`, rerun the project, restore. The pull request's results:

| Mutation | Expected |
| :-- | :-- |
| Match back on `ProductId` | 1 red |
| No fallback for an empty variant | 8 red |
| `From` drops the variant | 1 red |

## Scenario 3 - Through the running system (by hand)

With the whole stack up and a product that has two variants (for example a lens in two mounts), as a
customer: add both variants to the cart (`POST /api/cart/items` through the gateway on :5000), place the order
for one of them, wait for it to reach `Paid`, then `GET /api/cart`.

**Expected**: the line of the variant bought has gone down by the quantity ordered; the other line is
unchanged. Not recorded as run for this feature - the pull request's evidence is the test suite.
