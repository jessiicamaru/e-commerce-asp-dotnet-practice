# Quickstart: Somewhere for the Order to Go

How to prove the feature works, against the containerised stack. Contracts are in
[contracts/api.md](./contracts/api.md); the model in [data-model.md](./data-model.md).

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
# Nothing left over on the host ports - see CLAUDE.md
Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }   # must be empty
```

Identity must now publish **6056** (gRPC) as well as 5056, and Order must be given
`IDENTITY_GRPC_ADDRESS=http://identity:8081`.

## Automated

```bash
cd server
DB_PASSWORD=... dotnet test                                   # includes the new Identity.Tests
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh   # run once per Payment outcome
cd ../bruno && npx @usebruno/cli run --env local --env-var adminEmail=... --env-var adminPassword=...
```

## Scenarios

| # | Scenario | Expected |
| :-- | :--- | :--- |
| 1 | Save two addresses; list | first is default; list shows both, default first |
| 2 | Mark the second default; list | exactly one default — the second |
| 3 | Delete the default | the other becomes default |
| 4 | Save an address with no city / a 1-char postcode / country `XX` | `400`, each field named; wording says *not well-formed* |
| 5 | Save a 21st address | `409` |
| 6 | Two concurrent "set default" on different addresses | both return; exactly one default afterwards |
| 7 | `GET /api/orders/shipping-options` anonymously | two options with prices |
| 8 | Fill cart (item 9.99 × 3); checkout `{addressId, "express"}` | order: address copied, `shippingPrice` 15.00, `totalAmount` 44.97 |
| 9 | After scenario 8 settles | order `Paid`; payment amount **44.97**, not 29.97 |
| 10 | Edit, then delete, the address used in 8; read the order | order's address unchanged |
| 11 | Checkout with no `addressId`, customer has a default | default used |
| 12 | Checkout with no `addressId`, customer has none | `409` |
| 13 | Checkout body without `shippingOption` / with `"teleport"` | `400` |
| 14 | Customer B: `GET/PUT/DELETE /api/addresses/{A's id}` | `404` each — identical to a random id |
| 15 | Customer B checks out with A's `addressId` | `404`; B's cart unchanged; no order |
| 16 | Stop Identity; checkout | `503`; cart unchanged; no order |
| 17 | Admin: `preparing` on the `Paid` order, then again | `200` both; state `Preparing` |
| 18 | Admin: `shipment` with `VN123`, then again with `VN123`, then with `VN999` | `200`, `200`, `409`; reference stays `VN123` |
| 19 | Admin: `preparing` on a `Shipped` order / a `Failed` order / a `Submitted` order | `409` each; unchanged |
| 20 | Customer: `preparing` on their own order | `403` |
| 21 | Customer reads the order at each step | `Paid` → `Preparing` → `Shipped` + reference |
| 22 | An order placed before this feature | readable; `shippingAddress` null; status `Paid` if it said `Completed` |

**Negative control**: remove the `WHERE "Status" = @from` guard from the fulfilment update and
confirm the Order test for "preparing a Shipped order" fails; restore it.
