# Contract: Catalog's Pricing Service

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-21

The first synchronous cross-service interface in this system. Background:
[docs/architecture/service-to-service-communication.md](../../../docs/architecture/service-to-service-communication.md).

---

## Where it listens

| | REST | gRPC |
| :-- | :-- | :-- |
| host port | 5057 | **6057** |
| container port | 8080 | **8081** |
| protocol | HTTP/1.1 | **HTTP/2 cleartext (h2c)** |
| TLS | no | no |

Two ports, because one plaintext port cannot serve both: distinguishing HTTP/1.1 from HTTP/2 needs
ALPN, and ALPN is part of the TLS handshake. Measured — the existing endpoint answers `--http1.1`
with `proto=1.1 code=200` and refuses `--http2-prior-knowledge` outright.

No TLS because this is the shape a service mesh produces: the application speaks cleartext HTTP/2
and a sidecar handles mTLS. **If this ever leaves a trusted network, that stops being true** and the
port needs TLS or a mesh in front of it.

Clients address it as `http://catalog:8081` inside the compose network, `http://localhost:6057` from
the host.

---

## The call

```proto
service CatalogPricing {
  // Prices several products at once. One call per order, never one per line.
  rpc GetPrices (GetPricesRequest) returns (GetPricesResponse);
}

message GetPricesRequest {
  repeated string product_ids = 1;   // UUIDs as strings
}

message GetPricesResponse {
  repeated PricedProduct products = 1;
}

message PricedProduct {
  string product_id = 1;
  string name       = 2;
  string price      = 3;   // decimal as a STRING - see below
  bool   sellable   = 4;
}
```

### Why `price` is a string

Protobuf has no decimal type. `double` is the obvious choice and is wrong for money: it cannot
represent `0.1` exactly, and this system stores money as `decimal(18,2)` because the constitution
says so. A string carries the exact decimal across the wire and is parsed back with no rounding
anywhere in between.

`sint64` scaled by 100 is the other correct answer and encodes assumptions about scale into the
contract. The string does not.

### Why `sellable` rather than `is_active`

The caller needs to know *whether this can be bought*, not which flag Catalog happens to keep.
`IsActive` is Catalog's business; `sellable` is the question being asked. If Catalog later gains a
second reason something cannot be sold, the contract does not change.

### What is deliberately absent

**No availability, no stock, no count.** Catalog holds a `Availability` read model fed by events
from Inventory, and its own source carries a comment saying nothing may sell against it. Putting it
in this payload — a payload whose entire purpose is deciding a sale — would invite precisely that
misuse. Stock is answered by Inventory, under a row lock, at reservation time.

---

## Status codes, and what each one means

| Status | Meaning | Retried? |
| :-- | :-- | :-- |
| `OK` | every requested product is in the response | — |
| `NOT_FOUND` | at least one product id does not exist | **no** |
| `UNAVAILABLE` | Catalog could not answer | **yes** |
| `DEADLINE_EXCEEDED` | Catalog did not answer in time | **yes** |

`sellable: false` comes back as `OK`. It is an answer, not a failure — the caller decides what to do
with it.

> **`NOT_FOUND` and `UNAVAILABLE` must never be collapsed.** "This product does not exist" and "I
> could not find out whether this product exists" are different facts. A customer told the first
> when the second is true goes and checks a catalogue that is working perfectly.

---

## What the caller promises

1. **One call per order**, carrying every product id in it. Not one call per line
   ([research D5](../research.md)): per-line calls leave the handler holding partial state when the
   third line fails, and turn *refuse in full* into something a person has to remember.
2. **A 5-second deadline**, up to 3 attempts, backing off 200ms then 1s.
3. **Retry only `UNAVAILABLE` and `DEADLINE_EXCEEDED`.** Retrying a `NOT_FOUND` is three times the
   latency for the same refusal.
4. **Refuse the whole order** if any line does not resolve. No order row, no reservation, no event.
5. **Store what came back**, frozen onto the order line. Never a reference resolved at read time.
6. **Never fall back to anything the request supplied.** There is nothing to fall back to — the
   request no longer carries a price.

## What it does not promise

- **That the price will still be that in a minute.** It is the price at the moment of the call. The
  customer is charged what was frozen.
- **That the product is in stock.** A different question, a different owner, answered under a lock
  at reservation time.
- **That Catalog is reachable.** When it is not, the order is refused — and that is the change this
  feature makes to the system's failure profile.

---

## The failure this introduces

**Before**: Catalog down, orders still placed — at whatever price the customer said.
**After**: Catalog down, orders refused.

That is the right trade for a decision about money, and it is a real reduction in availability that
somebody should agree to rather than discover. Checkout now depends on Catalog, which it never did
before.

The refusal must answer **503**, not 500. A 500 says *we are broken*; the truth is *a dependency is
down, try again shortly*, and the difference decides whether a customer retries and whether a
monitor pages somebody.

**This needs a new exception type.** `Ecommerce.Shared/Exceptions/` contains exactly
`NotFoundException` and `ConflictException`, and `GlobalExceptionHandler` sends everything else to
500. A `DependencyUnavailableException` → 503 is part of this feature, and it touches a shared
building block that all seven services use.

---

## Health

The gRPC port serves the standard `grpc.health.v1.Health` service.

**Container health checks keep probing REST `/health` and are not changed** — `curl` cannot speak
gRPC health, so pointing the existing probe at the new port would leave it green and meaningless.

The consequence, stated rather than left to be found: Catalog can report healthy over REST while its
gRPC endpoint is broken, and no container probe would notice. What catches that is the end-to-end
check — an order would fail — which is an argument for trusting `verify-saga.sh` rather than adding
a second probe that duplicates it badly.
