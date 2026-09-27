# Quickstart: Validating the staff order search

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Scenario 1 - The tests (SC-001, SC-003)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~StaffOrderSearchTests"
cd ../client
npx vitest run src/pages/admin-order-search
```

**Expected**: 8 and 4 passed.

## Scenario 2 - Through the gateway (SC-002)

```bash
curl -fsS "http://localhost:5000/api/orders/staff?search=01a0&pageSize=5" -H "Authorization: Bearer $ADMIN" | jq '.items[].status'
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/orders/staff" -H "Authorization: Bearer $CUSTOMER"   # 403
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/orders/staff?search=01"  -H "Authorization: Bearer $ADMIN"   # 400
```

Bruno: `order/` seq 12-13 pass.

## Scenario 3 - The page

Sign in as the administrator, open `/admin/orders/find` ("Find an order" in the menu), type the first 8 characters of an
order id or a customer's email; pick "Failed". **Expected**: the orders, newest first, each with the customer's name and
email, opening the staff order view.

## Scenario 4 - Mutations (SC-004)

| Mutation | Expected red |
| :-- | :-- |
| The status filter ignored | `A_status_narrows_it_and_Paid_includes_the_legacy_Completed` |
| The id prefix ignored | `An_order_is_found_by_the_start_of_its_id_in_any_status` |
| The page sends the email to Order as `search` | "finds a customer's orders by their email", "asks Order nothing for an email nobody has" |
| `[Authorize(Roles = "Admin")]` loosened to `[Authorize]` | Bruno `a customer cannot search every order` (403) |
