# Quickstart: A compare-at price per variant

Against the compose stack rebuilt from this branch, through the gateway (`:5000`). `$TOKEN` is the variant's seller or
an administrator; `$P`/`$V` a product and its variant priced 990,000₫.

## 1. Set one

```bash
curl -s -X PUT -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"amount":1200000}' \
  localhost:5000/api/products/$P/variants/$V/prices/VND/compare-at | jq '.price, .compareAtPrice'
```

Expected: `990000`, `1200000`.

## 2. Refusals

- `{"amount": 990000}` or less: 400 ("above the price").
- `{"amount": 1200000.5}` in VND: 400 (dong has no decimal places).
- In USD on a variant with no dollar price: 400.

## 3. The shop

```bash
curl -s 'localhost:5000/api/products?onSale=true&pageSize=50' | jq '[.items[].id]'
```

Expected: `$P` among them; the card's `compareAtPrice` is 1200000. In the browser the card and the product page show
~~1.200.000₫~~ 990.000₫ -17%. "On sale" narrows the listing.

## 4. The price moves

Set the price to 1,250,000₫: the compare-at is gone (`null`). Set it back and the compare-at again.

## 5. Checkout is unchanged

Put the variant in a cart and ask for a quote: the line is priced 990,000₫, and no compare-at appears anywhere in the
quote or the order.
