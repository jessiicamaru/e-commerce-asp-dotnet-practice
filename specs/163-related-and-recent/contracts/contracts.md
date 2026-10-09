# Contracts: Related products and recently viewed

Through the gateway's `/api/products/**` route. No message, no gRPC change.

## `GET /api/products/{id}/related?limit=8` - anonymous, cached (specs/157)

200 `ProductResponse[]` in the listing's shape (language, currency, price, compare-at, availability, rating, shop name):
other products on the shelf from the product's category, then its department, by `rating_desc`, never the product.
Empty for an unknown or off-shelf product. 400 when `limit` is outside 1-12.

## `GET /api/products?ids=a&ids=b` - the listing, one more filter

Keeps only the products named (still only those on the shelf). 400 above 24 ids. Combines with the rest; the order is
the listing's, and the storefront re-orders by its own list.

## `sortBy=rating_desc`

The listing's new sort: rating count, then average, then name.
