# Quickstart: Validating delivery settings

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Scenario 1 - The tests (SC-001, SC-002)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~DeliverySettingsTests"
dotnet test tests/Ecommerce.Order.Tests           # every checkout test now prices from the seeded table
cd ../client
npx vitest run src/pages/admin-delivery src/components/order
```

**Expected**: green - 10 delivery tests; the page's 4, `TrackingLink`'s 3 and the order components.

## Scenario 2 - Through the gateway (SC-003)

Bruno `order/` seq 14-18 pass. By hand:

```bash
curl -fsS -X PUT http://localhost:5000/api/orders/delivery/options/express -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"name":"Express delivery","isActive":true,"sortOrder":1,"prices":{"VND":75000}}'
curl -fsS "http://localhost:5000/api/orders/shipping-options?currency=VND" | jq '.[] | {code, price}'
```

**Expected**: express at 75000; an order placed earlier still shows its old delivery price.

## Scenario 3 - The pages

`/admin/delivery`: change a price, turn an option off, add one; set the carrier's tracking address to
`https://carrier.example/track/{reference}`. Open a shipped order as its customer: the tracking reference is a link.

## Scenario 4 - Mutations (SC-004)

| Mutation | Expected red |
| :-- | :-- |
| `StoredShippingOptions` without `.Where(o => o.IsActive)` | `An_option_turned_off_is_neither_offered_nor_accepted` |
| The seed's `ON CONFLICT DO NOTHING` made `DO UPDATE SET "Name"` | `Configuration_adds_missing_options_and_changes_nothing_stored` |
| The last-option guard removed | `The_last_option_on_offer_cannot_be_turned_off` |
| `TrackingLink` ignoring the template | "links a reference to the carrier's page, named" |
