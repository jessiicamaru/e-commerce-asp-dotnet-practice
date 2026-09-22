# Quickstart: proving the shop has two price lists

Everything below runs against the containerised stack, through the gateway on `:5000`.

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

⚠️ **`--build` is not optional for this feature.** The Orchestrator relays the currency from
`OrderSubmittedEvent` into `ProcessPaymentCommand`; an Orchestrator image built before this change
drops it silently (research D4). A stale image is the one failure this feature has that nothing
downstream can detect.

## 1. A price is decided, not converted

Set two prices that are not a conversion of each other, then read them back.

```bash
# Admin token first (see verify-auth.sh for the login), then:
curl -s -X PUT "$BASE/api/products/$P/variants/$V/prices/USD" \
     -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
     --data-binary '{"amount": 1499}'

curl -s "$BASE/api/products/$P?currency=VND" | jq '.currency, .variants[0].price'   # 40000000, VND
curl -s "$BASE/api/products/$P?currency=USD" | jq '.currency, .variants[0].price'   # 1499,     USD
```

**What proves it**: 40,000,000 / 1,499 is not a round rate, and no rate anywhere in the system would
produce both. Pick amounts that are obviously not a conversion - that is the point of the check.

## 2. An unpriced variant is refused, not converted

```bash
curl -s -X DELETE "$BASE/api/products/$P/variants/$V/prices/USD" -H "Authorization: Bearer $ADMIN"

curl -s "$BASE/api/products/$P?currency=USD" | jq '.variants[0] | {price, sellable}'
# { "price": null, "sellable": false }   <- NOT 40000000, and NOT 1600
```

Then put it in a cart and try to check out in USD: **409**, naming the variant. Restore the price
afterwards, or step 3 fails for the right reason and the wrong test.

## 3. A checkout in dollars stays in dollars, all the way to the payment row

```bash
curl -s "$BASE/api/orders/quote?currency=USD&shippingOption=standard" -H "Authorization: Bearer $C" \
  | jq '{currency, subtotal, shippingPrice, taxTotal, totalAmount}'

curl -s -X POST "$BASE/api/orders?currency=USD" -H "Authorization: Bearer $C" \
     -H 'Content-Type: application/json' --data-binary '{"shippingOption":"standard"}' | jq '.id, .currency'
```

Then read the payment row, which is the one place the relay can have failed:

```bash
docker exec e-commerce-payment-db psql -U postgres -d ecommerce_payment_db \
  -c "SELECT \"OrderId\", \"Amount\", \"Currency\" FROM payments ORDER BY \"ProcessedAt\" DESC LIMIT 1;"
```

**The `Currency` must be `USD` and the `Amount` must equal the order's `totalAmount`.** A `Currency`
of `VND` with a dollar amount is the relay defect, and it is why this step reads the database rather
than an API.

## 4. Dong has no decimal places

```bash
curl -s "$BASE/api/orders/quote?currency=VND&shippingOption=standard" -H "Authorization: Bearer $C" \
  | jq '{subtotal, shippingPrice, taxTotal, totalAmount}'
```

Every number ends in `.00` - there is no such thing as half a dong - **and the parts still sum to the
total**, which the database's CHECK constraint enforces on the order written in step 3.

## 5. Language and currency are independent

```bash
curl -s "$BASE/api/products/$P?currency=USD" -H 'Accept-Language: vi' | jq '.name, .currency'
# a Vietnamese name, priced in USD
```

Four combinations, all valid. If switching one changes the other, D1 has been implemented wrongly.

## 6. The storefront

```bash
cd client && npm run dev
```

Switch currency and language with the two separate controls. The prices change and the labels do not,
then the labels change and the prices do not. A variant with no price in the chosen currency shows
"not sold in USD" and cannot be added to the cart.

## Negative controls

Each of these must make a test fail; if one passes, that test proves nothing.

| Break this | The test that must go red |
| :-- | :-- |
| Fall back to the default currency's amount when the price is missing | `VariantPriceTests.A_variant_without_a_price_in_the_currency_is_not_sellable` |
| Drop `Currency` from the saga relay into `ProcessPaymentCommand` | `SagaCurrencyRelayTests`, and step 3 above |
| Round VND tax to 2 decimals instead of 0 | `OrderTotalsTests.Vnd_amounts_have_no_fractional_part` |
| Derive the currency from the language | `RequestCurrencyTests.Currency_is_not_taken_from_the_language` |
