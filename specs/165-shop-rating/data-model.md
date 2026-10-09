# Data Model: A shop's rating

## No schema change

No table, column or index changes. The rating is derived on read:

```sql
SELECT sum("RatingAverage" * "RatingCount") / sum("RatingCount"), sum("RatingCount")
FROM products
WHERE "SellerId" = @seller AND "RatingCount" > 0 AND "RatingAverage" IS NOT NULL
```

rounded to two decimals, half away from zero; no row means no rating (`null`, count 0).

- `products.RatingAverage` (`numeric(3,2)`) and `RatingCount` are recomputed from the visible rows of `product_reviews`
  on every review write, hide and restore (specs/046), so a hidden review is out of both.
- Every product of the seller counts, on the shelf or not (research D2).
