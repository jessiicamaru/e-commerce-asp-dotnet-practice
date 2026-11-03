# Quickstart: A shop's rating

Against the compose stack rebuilt from this branch.

## 1. The field

```bash
curl -s "localhost:5000/api/shops/<seller id>" | jq '{shopName, ratingAverage, ratingCount}'
```

Expected: `ratingAverage` null and `ratingCount` 0 for a shop with no review (every seeded shop); after a delivered
order's review (Bruno's `seller` folder, or a customer through the storefront), the review's stars and 1.

## 2. Agreement

Signed in as that seller, `GET /api/products/insights/mine` reports the same `ratingAverage` and `ratingCount`.

## 3. The shop

Open `/shops/<seller id>`: under the name, the stars and "N reviews", or "No reviews yet". Open one of its products:
beside "Sold by <shop>", the same stars and count; on one of the shop's own products, nothing.
