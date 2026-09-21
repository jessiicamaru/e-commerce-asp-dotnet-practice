# Research: Somewhere for the Order to Go

Decisions for [spec.md](./spec.md). Each records what was chosen, why, and what was rejected. D1–D3
restate the three decisions the project owner took on 2026-09-21; the rest were made in planning.

---

## D1 — The address book lives in Identity

**Decision**: A `delivery_addresses` table in Identity's database, owned by Identity alone, served
over REST at `/api/addresses` (through the gateway) and read by Order over gRPC.

**Rationale**: Identity already owns the customer. A new service would own one table and add a
ninth database, a ninth image, a CI job's worth of wiring, and a gateway cluster.

**Cost, stated**: Identity stops being "signs tokens and nothing else". It gains a gRPC endpoint and
a second responsibility, and it becomes a **third synchronous dependency of checkout** (after Cart
and Catalog). If Identity is down, checkout is refused — as it already is today, since nobody can
sign in without it.

**Alternatives rejected**: a `Customer` service (above); Order owning the address book (Order would
become a profile store, and the address book is not about orders).

## D2 — Fulfilment is manual; the saga still ends at payment

**Decision**: Order gains two Admin-only transitions — *Paid → Preparing*, *Preparing → Shipped*
with a tracking reference — and an Admin list of orders by status. The saga does not change.

**Rationale**: Despatch as a saga step turns a seconds-long transaction into a days-long one and
changes what compensation means (you cannot un-send a parcel). Nothing in the system can
*perform* despatch yet, so automating it would automate a stub.

**Alternatives rejected**: a Shipping service with a simulated courier as a saga step (deferred, not
ruled out; it is the obvious next step once something real despatches).

## D3 — *Paid*, not *Completed*; the contract keeps its name

**Decision**: On `OrderCompletedEvent`, Order now records `Paid`. `Completed` stays in the enum so
existing rows and any rolled-back image still parse, but **no code writes it**; reads report it as
`Paid`. A data migration rewrites existing `Completed` rows to `Paid`.

**Rationale**: Renaming the event would change a record three services deserialize — a breaking
contract change for a word. Order's own status is Order's alone.

**Expand/contract check**: the enum already contained `Paid`, so an image from before this feature
parses a `Paid` row. If that older image is redeployed it writes `Completed` again for new
orders; the new image reads `Completed` as `Paid`, so rolling forward again needs no repair. Nothing
here strands an earlier image.

**Consequence**: `specs/003-order-lifecycle/data-model.md` documents `Paid` as unreachable by
design. That changes, and the document changes in the same PR. `verify-saga.sh`, `verify-auth.sh`
(if it asserts status) and the Bruno collection expect `Completed` and are updated, not relaxed.

---

## D4 — How Order reads an address: gRPC, the caller's token forwarded, an empty identity

**Decision**: A new proto, `address_reading.proto`, service `AddressReading`, one RPC:

```text
rpc GetMyAddress (GetMyAddressRequest { string address_id = 1; })   // "" = my default
    returns (GetMyAddressResponse { bool found = 1; Address address = 2; })
```

The request names **an address, never a user**. Identity identifies the caller from the bearer
token Order forwards — exactly the pattern `CartReading.GetMyCart` established in feature 010.

**Found, not NOT_FOUND**: "not found", "not yours" and "no default" all return `found = false`,
indistinguishably (FR-006). A gRPC `NOT_FOUND` status would work, but Order's reader already treats
status codes as transport outcomes (retry on `Unavailable`, 503 after); keeping "the answer is no"
in the message keeps it out of the retry logic. Same reasoning as `DescribeProducts` (010 D8).

**Checkout mapping**:

| Identity says | Checkout answers |
| :--- | :--- |
| found | proceeds |
| not found, an `addressId` was given | **404** "Address not found" — same for someone else's |
| not found, no `addressId` (default asked for) | **409** "No delivery address" |
| unreachable after retries | **503** |

**Alternatives rejected**: the client sending the address itself (the customer *is* the authority
on where to send, so it would be safe — but the acceptance criterion is "choose one of my saved
addresses", and it would make the address book decorative); `GetAddress(userId, addressId)` (any
caller on the network could read anyone's address — Principle IV one hop in).

## D5 — Identity serves gRPC on a second port, and stops calling `app.Run(url)`

**Decision**: Identity follows Catalog's exact shape: both endpoints declared together in
`ConfigureKestrel` — HTTP/1.1 on `5056` (container `8080`), HTTP/2 on `IDENTITY_GRPC_PORT`
(host default `5156`, container `8081`, published `6056`). The `app.Run("http://localhost:5056")`
fallback is removed.

**Rationale**: one plaintext port cannot serve both protocols (ALPN is part of TLS — measured in
feature 009). And `ListenAnyIP` *replaces* `ASPNETCORE_URLS`: configuring only the gRPC port
silently unbinds REST, which is exactly how Catalog went unhealthy on its first try. Declaring both,
once, is the fix already paid for.

## D6 — Delivery options belong to Order, as configuration

**Decision**: `Shipping:Options` in Order's configuration — code, name, price — with two defaults:
`standard` / "Standard delivery" / 5.00 and `express` / "Express delivery" / 15.00. Exposed at
`GET /api/orders/shipping-options`, anonymous.

**Rationale**: Order is what prices checkout, and freezing the option onto the order is Order's
job. Configuration rather than a table: two rows that change rarely do not need screens, and an
environment variable can override them.

**Route**: under `/api/orders/…` so the existing gateway route covers it. It cannot collide with
`GET /api/orders/{id}` because that route constrains `id` to a `guid`.

**Alternatives rejected**: Catalog owning them (Catalog prices products, not delivery); a new
Shipping service (see D2).

## D7 — Checkout gains a body again, and the option is required

**Decision**: `POST /api/orders` takes `{ "addressId": guid | null, "shippingOption": "express" }`.
`shippingOption` is **required**; `addressId` falls back to the default.

**Rationale**: an option that costs money is not defaulted silently. The body still carries **no
items, no prices and no user** — what is bought still comes from the cart, what it costs from
Catalog and the option table, who is buying from the token. `verify-saga.sh`'s hostile-body check
(extra `items`, `unitPrice`, `userId` fields) continues to prove those are ignored.

**Breaking**: a body-less `POST /api/orders` becomes a 400. Every caller in the repository —
`verify-saga.sh`, `verify-auth.sh`, the Bruno collection — changes in the same PR.

## D8 — The order's copy of the address: nullable columns, not an owned table

**Decision**: The address and the delivery choice are **columns on `orders`**, all nullable:
`ShipTo…` (recipient, lines, city, region, postal code, country, phone), `ShippingOptionCode`,
`ShippingOptionName`, `ShippingPrice`, `TrackingReference`. Mapped as an EF owned type
`ShippingAddress` for the address part.

**Rationale**: exactly one per order, never shared, never queried on its own — columns, not a table.
Nullable because orders placed before this feature have none (FR-019); **additive** under the
constitution's schema rule.

`TotalAmount` becomes items + shipping. The subtotal remains derivable from the lines; #21 owns a
proper breakdown.

## D9 — Fulfilment transitions are guarded updates

**Decision**: each transition is one statement:

```text
UPDATE orders SET "Status" = @to, "UpdatedAt" = now() [, "TrackingReference" = @ref]
 WHERE "Id" = @id AND "Status" = @from
```

1 row → done. 0 rows → re-read: already at `@to` → **200, no change** (FR-016; for *Shipped*, only if
the tracking reference is the same — a different one is **409**); any other state → **409**; no
such order → **404**.

**Rationale**: the same guarded-transition pattern Order already uses to settle from the saga
(Principle III). A read-then-write would let two staff clicks both "succeed".

## D10 — "Exactly one default" is enforced by the database

**Decision**: a partial unique index `ON delivery_addresses ("UserId") WHERE "IsDefault"`, and every
address write for a user runs after `SELECT … FROM users WHERE "Id" = @me FOR UPDATE`, which
serialises one customer's address changes (and makes the 20-address limit exact).

**Rationale**: the invariant is the kind the constitution says MUST also be a constraint. The lock
exists because "demote the old default, promote the new one" is two rows, and two concurrent
set-default calls would otherwise race into the index and fail one caller with a 500.

## D11 — Validation, and what it does not claim

**Decision** (FluentValidation, in Identity's Application layer):

| Field | Rule |
| :--- | :--- |
| `recipientName` | required, ≤ 100 |
| `line1` | required, ≤ 200; `line2` optional, ≤ 200 |
| `city` | required, ≤ 100; `region` optional, ≤ 100 |
| `postalCode` | required, 2–16 chars of letters, digits, spaces and hyphens |
| `country` | required, two letters, a region .NET recognises (`RegionInfo`) |
| `phone` | optional, ≤ 30, digits, spaces and `+-()` |

Responses and docs say **"well-formed"**, never "valid" or "verified". A well-formed postcode may not
exist; a parcel may not reach an address that passes every rule. Checking deliverability needs a
postal database this project does not have (FR-004).

## D12 — Identity gets its first test project

**Decision**: `server/tests/Ecommerce.Identity.Tests`, against a throwaway database on Identity's
PostgreSQL (5435), following `CartTestFixture`.

**Rationale**: Principle V — the one-default invariant, the 20-address limit and ownership are
database guarantees, and must be tested against a database. Identity has had no tests at all; this
feature adds the first behaviour to it that is not simply "log in".

## D13 — Where the end-to-end proof lives

**Decision**: `verify-saga.sh` gains, on the approve branch: save an address, check out with
express, assert the order's recorded address and `shippingPrice`, assert the **payment amount**
equals items + express (the charge, not only the order row), assert `Paid`; then, as Admin, move it
to Preparing and Shipped and read it back as the customer. `verify-auth.sh` gains the cross-customer
checks with two real signed tokens (SC-003).
