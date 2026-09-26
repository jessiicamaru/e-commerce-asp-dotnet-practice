# Quickstart: Validating the release of a deleted product's holds

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d        # PostgreSQL for Inventory on 5437
```

## Scenario 1 - The tests (US1, US2, SC-001 to SC-004)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Inventory.Tests
```

**Expected**: all green, including in `ForgetProductTests`:
- `A_held_reservation_of_a_deleted_variant_is_released_with_its_stock`
- `Only_the_deleted_variants_holds_are_released_and_the_rest_of_the_order_settles`
- `The_sweeper_finds_nothing_of_a_deleted_variant_to_expire`
- `A_settled_reservation_stays_as_the_orders_history`
- `A_cancellation_after_the_variant_was_deleted_settles_without_a_shelf_to_return_to`

## Scenario 2 - In the database, end to end

With the stack running, as an administrator: list a product and stock it, place an order and keep it from settling
(for example with Payment stopped), then `DELETE /api/products/{id}` through the gateway.

```sql
-- psql -h localhost -p 5437 -U $DB_USER ecommerce_inventory_db
SELECT "Status", "SettlementReason" FROM stock_reservations WHERE "ProductId" = '<variant id>';
SELECT count(*) FROM stock_items WHERE "ProductId" = '<variant id>';
```

**Expected**: `Released | Product deleted`, and `0`.

## Scenario 3 - Mutations (SC-003)

| Mutation | Expected red |
| :-- | :-- |
| Remove the `ReleaseHeldAsync` call from `ForgetProductCommandHandler` | the three US1 tests |
| Drop `&& x.Status == ReservationStatus.Held` from `ReleaseHeldAsync` | `A_settled_reservation_stays_as_the_orders_history` |
