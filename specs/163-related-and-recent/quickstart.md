# Quickstart: Related products and recently viewed

Against the compose stack rebuilt from this branch, with the seeded catalogue.

## 1. Related

```bash
curl -s "localhost:5000/api/products/<A7 IV id>/related?currency=VND" | jq '[.[].name]'
```

Expected: other cameras first (same category), most reviewed first, never the A7 IV; filled from the department when
the category has fewer than 8. An off-shelf product's id: `[]`. `limit=13`: 400.

## 2. Ids

```bash
curl -s "localhost:5000/api/products?ids=<A>&ids=<B>&ids=<unknown>" | jq '[.items[].id]'
```

Expected: A and B only.

## 3. The shop

Open three products in turn: under each, "Related products" and "Recently viewed" (without the current one); the
landing page shows the three, most recent first. Clear the browser's storage: the recently viewed rows are gone.
