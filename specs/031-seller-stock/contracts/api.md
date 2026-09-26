# Contracts: A seller can stock what they sell

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md. This one file holds both the HTTP change and the gRPC contract; no
> message changed (see the end).

## The REST change is one attribute and two refusals

| Method | Path | Before | After |
| :-- | :-- | :-- | :-- |
| `PUT` | `/api/stock/{variantId}` | `[Authorize(Roles = "Admin")]` | `[Authorize(Roles = "Seller,Admin")]`, with ownership decided in the handler |

⚠️ **Opening the attribute is not the feature, and closing it too early hides the feature.**
specs/027 shipped with the ownership checks unreachable because the controller still said
`Admin` — every unit test passed and a real seller was refused at the door. Same trap, same service
family, second time.

The body and the path are unchanged. **No seller id anywhere**: whose call this is comes from the
token.

### What it answers now

```text
PUT /api/stock/{variantId}   as the owning seller        -> 200
PUT /api/stock/{variantId}   as an administrator         -> 200   (unchanged)
PUT /api/stock/{variantId}   as another seller           -> 404
PUT /api/stock/{variantId}   for a variant that does not exist -> 404   (same words)
PUT /api/stock/{variantId}   as a customer               -> 403   (at the door, no ownership read)
```

The two 404s are deliberately indistinguishable. A third case is **not**:

```json
{ "status": 404, "detail": "Product '…' is not registered in inventory." }
```

which is the stock row not having arrived yet (research D6) and is only ever returned **after**
ownership has been confirmed — so a seller seeing it knows to retry, and a seller seeing the flat
404 knows not to.

### As built (from the code at #71)

| Case | Status | `detail` |
| :-- | :-- | :-- |
| owner, row present | `200` | body is `StockResponse`: `productId`, `sku`, `quantityOnHand`, `quantityReserved`, `quantityAvailable` |
| administrator | `200` | Catalog is **not** asked |
| another seller's variant, the shop's own product, or no such variant | `404` | `Product with ID '{id}' was not found.` |
| owner, stock row not arrived | `404` | `Product '{id}' is not registered in inventory.` |
| owner, value below reserved | `409` | `Cannot set quantity on hand to {n}: {m} unit(s) are currently reserved for orders.` |
| Catalog unreachable after three attempts | `503` | `The catalogue could not be reached, so it is not possible to tell whether this product is yours. Try again in a moment.` |
| customer without `Seller` | `403` | at the door |
| no token | `401` | |

Request body, unchanged: `{ "quantityOnHand": 4 }`. The route parameter is still named `productId` in
`StockController` and holds a variant id (specs/020).

## The new gRPC contract

A new proto rather than a method on `CatalogPricing`: "who owns this" is an input to an
**authorization** decision, and burying it in a service called Pricing means the next person looking
for the rule does not find it.

```proto
syntax = "proto3";
option csharp_namespace = "Ecommerce.Contracts.Grpc";
package catalog.ownership.v1;

// Who a listing belongs to, answered by the service that owns the answer.
//
// This exists so Inventory can refuse a seller who is stocking somebody else's product.
// It is asked LIVE on every check and deliberately not cached: an authorization answer
// that is seconds out of date refuses a seller their own product with the same 404 that
// means "not yours", and nothing can tell the two apart. See specs/031 research D2.
service CatalogOwnership {
  rpc GetVariantOwners (GetVariantOwnersRequest) returns (GetVariantOwnersResponse);
}

message GetVariantOwnersRequest {
  repeated string variant_ids = 1;
}

message VariantOwner {
  string variant_id = 1;
  string product_id = 2;
  // Empty when the product belongs to the shop itself - an administrator's listing
  // (specs/027). Empty is NOT "unknown": a variant that does not exist is simply absent
  // from the response.
  string seller_id = 3;
}

message GetVariantOwnersResponse {
  repeated VariantOwner owners = 1;
}
```

**Plural, for one caller.** `PriceVariants` learned this: a per-item call leaves the caller holding
partial state when the third one fails. Only one variant is asked about today, and the shape does
not need changing when a bulk stock editor arrives.

**A missing variant is absent, not an error.** The caller turns absence into its own 404 with its
own wording, which is what keeps the two 404s identical.

## Traffic direction

| From | To | Port | Why |
| :-- | :-- | :-- | :-- |
| Inventory | Catalog | 6057 (h2c) | new. Ownership, on stock writes only |

⚠️ Inventory has no gRPC client today and no `ICurrentUser`. Both arrive with this feature, and the
token is **forwarded** on the call the way Order already forwards it to Cart and Identity — except
here Catalog is only being asked a fact, so the forward is for tracing rather than for identity.
Inventory decides; it does not ask Catalog to decide for it.

> **Correction (2026-09-27 backfill)**: as built, the token is **not** forwarded. `GrpcProductOwnership`
> calls `GetVariantOwnersAsync` with a deadline and a cancellation token only, and the client is
> registered with `AddGrpcClient` and an address, no interceptor or call credentials. That matches the
> sentence above in substance — Catalog is asked a fact and does not need to know who is asking — but
> the forwarding described here was not built.

## Unchanged, and must stay so

- `GET /api/stock` and `GET /api/stock/{id}` stay anonymous.
- `SetStockOnHandCommand` keeps its `FOR UPDATE` lock, its refusal to go below `QuantityReserved`,
  and its `StockAvailabilityAnnouncer` call. Six handlers move stock and every one announces
  (specs/004); this adds no seventh.
- `ProductCreatedEvent`, `ProductVariantCreatedEvent` and `ProductDeletedEvent` are untouched.

## Messages

None added or changed. The feature has no `messages.md` for that reason: the only asynchronous
behaviour it relies on is the existing `ProductCreatedEvent` → stock row (why the "not arrived yet" 404
exists) and the existing `StockAvailabilityChangedEvent` announced on every stock write.

## Configuration

Inventory reads Catalog's gRPC address from `Catalog:GrpcAddress`, then `CATALOG_GRPC_ADDRESS`, then
`http://localhost:5157` (the `start-dev` port). Compose sets `CATALOG_GRPC_ADDRESS: http://catalog:8081`
and makes Inventory `depends_on` a healthy Catalog.
