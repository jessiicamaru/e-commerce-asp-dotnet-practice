# Quickstart: Validating that a ban closes a seller's shop

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d
```

## Scenario 1 - The tests (SC-001 to SC-003)

```bash
cd server
dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests|FullyQualifiedName~ShopApplicationTests"
dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SellerSuspensionTests"
cd ../client && npx vitest run src/pages/admin-users
```

**Expected**: green, including `Banning_a_seller_closes_their_shop_and_lifting_the_ban_reopens_it`,
`Banning_somebody_who_does_not_sell_closes_no_shop`, `Locking_a_seller_leaves_their_shop_open`,
`A_banned_applicant_is_not_given_a_shop`, the five `SellerSuspensionTests`, and "says a banned seller's shop is closed".

## Scenario 2 - Through the gateway (SC-005)

With Identity and Catalog rebuilt from this branch, run the Bruno collection. **Expected**: `seller/` seq 18-22 pass -
ban (200), the product 404 to a shopper, lift (200), the product 200 again, the seller signs in again.

By hand:

```bash
curl -fsS -X POST "http://localhost:5000/api/users/$SELLER_ID/ban" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"reason":"Fakes"}'
sleep 2; curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$PRODUCT_ID"   # 404
curl -fsS -X POST "http://localhost:5000/api/users/$SELLER_ID/unban" -H "Authorization: Bearer $ADMIN"
sleep 2; curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$PRODUCT_ID"   # 200
```

## Scenario 3 - Mutations (SC-004)

| Mutation | Expected red |
| :-- | :-- |
| The ban publishes no `SellerSuspensionChangedEvent` | `Banning_a_seller_closes_their_shop_and_lifting_the_ban_reopens_it` |
| `OnShelf => IsListed && IsActive` (suspension ignored) | `A_suspended_sellers_products_leave_the_shelf_everywhere` |
| The upsert without its `WHERE ... SuspensionChangedAt < EXCLUDED...` guard | `An_older_decision_arriving_late_changes_nothing`, `Reinstating_puts_them_back_and_tells_whoever_saved_one_in_stock_once` |
| The listing's SQL without `!p.SellerSuspended` | `A_suspended_sellers_products_leave_the_shelf_everywhere` |
