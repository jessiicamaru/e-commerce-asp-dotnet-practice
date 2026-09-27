# Data Model: Staff find any order

**No table, column, index or migration changes.** The search reads `orders` (Order, `ecommerce_order_db`, 5434).

| Column | Used for |
| :-- | :-- |
| `Id` | the prefix match, as text (`"Id"::text LIKE 'prefix%'`) |
| `UserId` | the customer filter, and on each row |
| `Status` | the status filter; `Paid` also matches `Completed` |
| `CreatedAt` | newest first |
| `TotalAmount`, `Currency`, `FailureReason` | on each row |
| `order_items` (count), `order_shipments` (counts) | on each row, as correlated counts |

```sql
SELECT ... FROM orders o
 WHERE (@status IS NULL OR o."Status" IN (@status, @also))
   AND (@customer IS NULL OR o."UserId" = @customer)
   AND (@prefix IS NULL OR o."Id"::text LIKE @prefix || '%')
 ORDER BY o."CreatedAt" DESC
 OFFSET (@page - 1) * @size LIMIT @size;
```
