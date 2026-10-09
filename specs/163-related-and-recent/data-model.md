# Data Model: Related products and recently viewed

## Server - no schema change

No table, column or index changes. Related products read `products` through the listing's query (category, department,
on the shelf); `ids` is one more filter on the primary key.

## Browser

`localStorage["recentlyViewed"]`:

```json
["<productId>", "<productId>", "..."]
```

- Most recent first, each id once, at most 12.
- Written when a product page opens; unreadable content reads as empty.
