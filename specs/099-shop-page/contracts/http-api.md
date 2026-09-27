# HTTP contract: A shop has a page

Every route goes through the gateway (`:5000`). The gateway gains `catalog-shops-route`, which sends
`/api/shops/{**catch-all}` to `catalog-cluster`.

## `GET /api/shops/{sellerId}`: anyone (Catalog)

`200`:

```json
{ "sellerId": "01a0d189-…", "shopName": "Mai Lens Hà Nội", "description": "Used Fujifilm bodies.", "productCount": 3 }
```

- `description` is null when the seller has written none.
- `productCount` is the number of the seller's products on the shelf now.
- `404` `Shop not found.` covers three cases with one message: an unknown id, a seller whose name Catalog has not received
  yet, and a suspended seller.

## `GET /api/products?sellerId={id}`: anyone (Catalog)

This is the existing listing with one more filter. Every other parameter still applies (`categoryId`, `searchTerm`,
`sortBy`, paging), and only products on the shelf are listed. An unknown id returns an empty page, not a 404.

## `PUT /api/sellers/me/description`: Seller (Identity)

```json
{ "description": "Used Fujifilm bodies." }
```

- `200` returns the profile, `{ "sellerId", "shopName", "createdAt", "description" }`. The description comes back
  trimmed, or null if it was cleared.
- `400` when it is longer than 500 characters (`errors.Description`).
- `401` without a token.
- `404` `This account does not sell on the shop.` for a signed-in person who has no seller profile.

The token says who is describing. There is no seller id in the address or the body.

## `GET /api/sellers/me`: Seller (Identity)

The response now also carries `description`.
