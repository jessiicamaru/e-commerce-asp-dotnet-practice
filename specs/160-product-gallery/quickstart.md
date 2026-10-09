# Quickstart: A gallery of photographs per product

Against the compose stack rebuilt from this branch, through the gateway (`:5000`). `$SELLER` is a seller's token and
`$P` one of their products, approved.

## 1. Add photographs

```bash
for f in a.png b.png c.png; do
  curl -s -X POST -H "Authorization: Bearer $SELLER" -F "file=@$f" localhost:5000/api/products/$P/photos | jq '.imageUrl, (.photos | length), .reviewStatus'
done
```

Expected: when `$P` had no cover the first file becomes `imageUrl` (photos 0), then photos 1 and 2; `reviewStatus`
is `Pending` after the first change.

## 2. Reorder and choose the cover

```bash
curl -s -X PUT -H "Authorization: Bearer $SELLER" -H 'Content-Type: application/json' \
  -d '{"photoIds":["<second>","<first>"]}' localhost:5000/api/products/$P/photos/order | jq '[.photos[].id]'
curl -s -X POST -H "Authorization: Bearer $SELLER" localhost:5000/api/products/$P/photos/<first>/cover | jq '.imageUrl, [.photos[].id]'
```

Expected: the order as sent; after the cover change `imageUrl` has a new `v`, and the old cover is a photograph where
`<first>` was.

## 3. Refusals

- An eleventh photograph: 409 naming the limit.
- A reorder missing one id, or naming another product's: 400, order unchanged.
- Another seller: 404 on every write.
- An anonymous read of a photograph of a product off the shelf without `k`: 404.

## 4. The shop

Approve `$P`, open `localhost:8088/products/$P`: the cover large, three thumbnails; choose one, it is shown. On a phone
width the thumbnails scroll sideways. The listing card shows the cover.

## 5. Nothing left behind

`GET /api/products/images/orphans` as the administrator: `orphans` empty, `liveKeys` counting the photographs. Delete
`$P`: the report is still empty.
