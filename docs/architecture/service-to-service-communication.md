# How Services Talk to Each Other

**Created**: 2026-09-21 | **Updated**: 2026-09-24 | **Status**: the decision was taken and built — see [specs/009-catalog-owns-price](../../specs/009-catalog-owns-price/); later edges in specs/010, 011 and 031

> **Resolved.** gRPC over h2c on a second port, built in feature 009. What follows describes the
> system *before* that, which is still worth reading because it is why the decision went the way it
> did — and because two of its predictions were tested rather than assumed:
> `Http1AndHttp2` on a plaintext endpoint does **not** serve h2c on .NET 10 (measured, with a
> control), and calling `ListenAnyIP` at all **replaces** `ASPNETCORE_URLS` rather than adding to
> it, which unbound REST the first time it was tried.

## Today: four gRPC services, five edges

Every synchronous call between services is gRPC over h2c on the serving service's second port. The
generated list of methods is [reference/grpc.md](../reference/grpc.md).

| gRPC service | Served by | Called by | Method in use | Since |
| :-- | :-- | :-- | :-- | :-- |
| `CatalogPricing` | Catalog | Order | `PriceVariants` - all-or-nothing, for a sale | feature 009, variants in specs/020 |
| `CatalogPricing` | Catalog | Cart | `DescribeVariants` - per variant, for display | feature 010, variants in specs/020 |
| `CartReading` | Cart | Order | `GetMyCart` - empty request, the customer's token forwarded | feature 010 |
| `AddressReading` | Identity | Order | `GetMyAddress` - an address id, the customer's token forwarded | feature 011 |
| `CatalogOwnership` | Catalog | Inventory | `GetVariantOwners` - who a variant belongs to | specs/031 |

`CatalogPricing` also still serves `GetPrices` and `DescribeProducts`, the product-level methods from
before variants existed. Nothing in the current code calls them; they stay so that an older Order or
Cart image keeps working against a newer Catalog during a rollback.

| Port | Identity | Catalog | Cart |
| :-- | :-- | :-- | :-- |
| gRPC under `start-dev` (host) | `5156` | `5157` | `5162` |
| gRPC inside a container | `8081` | `8081` | `8081` |
| gRPC published by compose on the host | `6056` | `6057` | `6062` |

Callers find the address in `Catalog:GrpcAddress` / `CATALOG_GRPC_ADDRESS`, `Cart:GrpcAddress` /
`CART_GRPC_ADDRESS` and `Identity:GrpcAddress` / `IDENTITY_GRPC_ADDRESS`, defaulting to the
`start-dev` ports. Order's three clients and Inventory's make up to three attempts, each with a
5-second deadline, on `Unavailable` or `DeadlineExceeded` (Inventory's also on `Internal`), and then
throw `DependencyUnavailableException`, which the shared exception handler answers with **503**.
Cart's client makes one attempt with a 3-second deadline and, when Catalog does not answer, shows the
cart with prices marked unavailable instead of failing. None of the answers is cached.

What each edge costs when its callee is down:

| Callee down | Consequence |
| :-- | :-- |
| Catalog | nobody can check out; a seller cannot set stock; carts render with prices marked unavailable |
| Cart | nobody can check out |
| Identity | nobody can check out (and nobody can sign in) |

The rest of this document is the history of how the system got here, in order.

## Before feature 009: nothing called anything

Seven services, and **not one of them called another synchronously**. Verified, not assumed:

```bash
$ grep -rln "HttpClient\|IHttpClientFactory\|AddHttpClient" server/src/Services --include=*.cs
(nothing)

$ grep -rln "IRequestClient\|AddRequestClient" server/src --include=*.cs
(nothing)
```

Every cross-service interaction is a message: published through the transactional outbox, delivered
by RabbitMQ, consumed by whoever is bound to it. The saga coordinates; nobody waits on anybody.

That is not an accident. It is what makes a service able to be down without taking the others with
it, and it is why `Ecommerce.Contracts` — pure records, **zero package references**, verified — is
the only coupling the constitution allows.

### The one thing that looks like an exception, and is not

Catalog holds `Product.Availability`, fed by `StockAvailabilityChangedEvent` from Inventory. That is
a **read model**, and the rule around it is absolute:

> **Nothing may sell against it** — checkout reserves under `FOR UPDATE` against Inventory's row,
> and a read model fed by messages is seconds behind by design.

Copy for display, ask the owner for a decision. Remember that sentence; it is the whole of the next
section.

---

## Why this is about to change

[#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18): the unit price on an
order comes from the request body. A customer can buy a 40,000,000 item for 1, and the system
reserves the stock, charges the 1, and completes the order.

Fixing it means Order must learn the real price — from Catalog, which owns it, **at the moment the
sale is decided**. Not from a replicated copy, for exactly the reason quoted above: a price read
model fed by events would be seconds behind, and selling against it repeats the mistake
[#4](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/4) already fixed for
stock.

So this will be **the first synchronous cross-service call in the system**, and it will sit on the
critical path of checkout.

### The consequence worth naming

Today Catalog can be down and orders still get placed — at the wrong price, but placed. Afterwards,
**Catalog down means nobody can order**.

For a decision about money that is the right trade: refusing is better than charging wrongly. But it
is a trade, it should be made deliberately, and it needs a timeout and a bounded retry, because a
slow Catalog now makes checkout slow for everyone.

---

## Does a synchronous call break Principle I?

No, and the distinction matters.

Principle I forbids reading another service's **database** and calling into another service's
**internals**. `GET /api/products/{id}` is a public interface — it is what Catalog exists to offer.
Asking it a question over its published API is the opposite of reaching around it.

What Principle I does still forbid, and what this must not become: Order keeping its own table of
prices, or reading `ecommerce_catalog_db` directly because it is faster.

---

## REST or gRPC

Both work. gRPC fits this particular call well — internal, frequent, strongly typed, no browser
involved — and it is the more interesting one to learn. It is also **more expensive here**, and the
expense is concrete rather than philosophical.

### What HTTP/2 costs in this codebase, measured

gRPC requires HTTP/2. Every service currently binds plaintext HTTP (`http://+:8080` in containers).
Tested against the running stack, from inside the compose network:

```bash
$ docker run --rm --network server_default curlimages/curl \
    -s -o /dev/null -w 'proto=%{http_version} code=%{http_code}\n' \
    --http1.1 http://catalog:8080/health
proto=1.1 code=200

$ docker run --rm --network server_default curlimages/curl \
    -s -o /dev/null -w 'proto=%{http_version} code=%{http_code}\n' \
    --http2-prior-knowledge http://catalog:8080/health
proto=0 code=000          # connection failed
```

**The endpoints serve HTTP/1.1 and nothing else.** A gRPC client would not get a connection.

The reason one port cannot serve both without TLS: distinguishing HTTP/1.1 from HTTP/2 needs ALPN,
and ALPN is part of the TLS handshake. Without TLS the server has to be told which protocol it
speaks.

> **Verified above**: the default plaintext endpoint is HTTP/1.1 only.
>
> **`Http1AndHttp2` — since verified, on .NET 10, with a control.** A minimal app was given two
> plaintext endpoints and probed from a container:
>
> ```text
> port 7311, Http1AndHttp2   --http1.1               -> proto=1.1 code=200
> port 7311, Http1AndHttp2   --http2-prior-knowledge -> proto=0   code=000   (refused)
> port 7312, Http2 only      --http2-prior-knowledge -> proto=2   code=200   (works)
> ```
>
> Microsoft's documented behaviour holds: it does **not** serve h2c, so the second port is required
> rather than preferred. The control matters as much as the result — without it, `proto=0` would
> equally well have meant a broken harness.
>
> **And one thing nobody documented loudly enough**: calling `ListenAnyIP` at all **replaces**
> `ASPNETCORE_URLS` rather than adding to it. Configuring only the gRPC endpoint unbound REST — the
> container listened on 8081 alone and went unhealthy — and Kestrel says so in a warning that is
> easy to scroll past: `Overriding address(es) 'http://+:8080'`. Both endpoints must be declared
> together.

### What real systems do

Worth knowing, because the common advice and the common practice differ:

- **Most systems of this size do not use gRPC at all.** REST between services, messages for
  asynchronous work. Not ignorance — the codegen, the extra protocol and the tooling do not pay for
  themselves across a handful of calls.
- **Systems that do use gRPC are usually in Kubernetes with a service mesh**, and the application
  binds **plaintext h2c** while a sidecar handles mTLS. The app never sees a certificate. This is
  the most common real shape of gRPC, and it is why "bind h2c with no TLS" sounds reckless but is
  standard — somebody else is encrypting.
- **Without a mesh**, certificates are managed by something like cert-manager and services bind real
  HTTPS.
- **Many gRPC systems do not serve REST from the service at all.** The public surface is a gateway
  or BFF; everything behind it speaks gRPC only, so the two-protocols-one-port problem never arises.

This project has each service serving its own public REST API, which is what puts it in the
dual-surface case.

### The recommendation

**A second port, h2c, no TLS.** Catalog keeps 5057 for REST and adds a gRPC port configured
`HttpProtocols.Http2`.

Not because it is optimal, but because it is **the shape a service mesh would give you anyway** —
the app speaks h2c and encryption is somebody else's layer. It skips the genuinely painful part
(certificates in development, in containers, and in CI) and that part teaches nothing about gRPC. If
this ever moves to Kubernetes with a mesh, the code barely changes.

It costs: a port in the service map, in `docker-compose.app.yml`, and in `CLAUDE.md`.

### Where the pieces go

```text
Ecommerce.Contracts.Grpc/                              ← .proto and generated code, a NEW project
Order.Application/Common/Interfaces/ICatalogPrices.cs  ← the interface; knows nothing about gRPC
Order.Infrastructure/Catalog/GrpcCatalogPrices.cs      ← the implementation, holds the channel
```

**`.proto` does not go in `Ecommerce.Contracts`.** That project has zero package references today
and `CLAUDE.md` says so; adding `Grpc.Tools` and `Google.Protobuf` would end that. The separation is
also honest: a gRPC contract and a message contract are different kinds of coupling — one
synchronous, one not.

**The client lives in Infrastructure**, behind an interface declared in Application. Principle II:
Application depends on abstractions only, *never a transport package*. This is the same shape as
`IPaymentGateway` / `StubPaymentGateway`, and it means swapping gRPC for REST later touches one
file.

### Things that behave differently, and surprise people

| | REST | gRPC |
| :-- | :-- | :-- |
| Health check | `GET /health` | `grpc.health.v1.Health` — a different protocol; container health checks need `grpc_health_probe` or keep probing REST |
| Poking by hand | `curl` | `grpcurl`, and remember `-plaintext` |
| CI | nothing extra | codegen runs inside `dotnet build`, but `.proto` must reach the image — check `.dockerignore` |
| The gateway | routes it | not involved; this call is internal and does not pass through YARP |

---

## Feature 010: the second call, and why it carries the customer's token

The cart ([specs/010-customer-cart](../../specs/010-customer-cart/)) added a second synchronous call
on checkout's path. `POST /api/orders` now takes no body; Order asks Cart for the cart over gRPC
(`CartReading.GetMyCart`, Cart's port 6062 on the host, 8081 in a container), then prices it
through Catalog exactly as before.

```text
customer ──POST /api/orders (bearer token)──▶ Order
                                                │ gRPC GetMyCart, same bearer token as metadata
                                                ├──────────────────────────────▶ Cart   (what)
                                                │ gRPC GetPrices
                                                ├──────────────────────────────▶ Catalog (how much)
                                                ▼
                                   order row + OrderSubmittedEvent, one transaction
```

**The request message is empty.** Cart learns whose cart to return from the token Order forwards, the
same way every service learns who the caller is. A `GetCart(userId)` would have been easier to write
and would have let anything on the network read anybody's cart — `UserId` in the body, one hop
further in. Principle IV applies between services as much as at the edge.

**Checkout now depends on Catalog and Cart.** Either being down stops orders; both are retried a
bounded number of times and then refused with 503, never guessed. Cart's own read of prices is the
opposite: when Catalog does not answer, the cart still renders, with prices marked unavailable and
checkout disabled — a customer can look at what they chose while the shop is degraded.

**The cart stores no price**, so the question left open above — what happens when a price changes
between adding and paying — has the simplest answer: the cart always shows today's price, and
checkout charges today's price. There is no stored number to disagree with.

The cart's *removal* of ordered lines is not synchronous: it listens for `OrderCompletedEvent`. That
decision, and why it needs `OrderSubmittedEvent` too, is in the spec's research D1–D3.

---

## Feature 011: the third dependency

The delivery address joined the same shape: `AddressReading.GetMyAddress` on Identity's second port
(`6056` on the host, `8081` in a container), an **address id and never a user** in the request, the
customer's token forwarded. "Not found", "not yours" and "you have no default" all come back as
`found = false` — in the message, not as a gRPC status, so the answer is never mistaken for a
transport failure worth retrying.

Checkout now waits on **three** services before it writes anything: Cart (what), Identity (where) and
Catalog (how much). Each is retried a bounded number of times and then refused with 503. Details in
[specs/011-order-shipping](../../specs/011-order-shipping/).

---

## Specs/020: the same edges, asking about variants

When a product came to be sold in variants, the thing being priced changed from a product to a
variant. Rather than change `GetPrices` and `DescribeProducts`, Catalog gained two new methods,
`PriceVariants` (Order) and `DescribeVariants` (Cart), and the old ones were left in place. A changed
message would have broken an older image during a rollback; a new method breaks nothing. Since
specs/034 and 036 the priced variant also carries `seller_id` and `seller_name`, which Order freezes
onto the line. `seller_id` is proto3 `optional`: *empty* means the shop's own goods, *absent* means a
Catalog too old to say, and Order then records no seller and logs a warning rather than refuse the
checkout.

---

## Specs/031: the fourth service - who owns a variant

Until specs/031 Inventory called nobody. Letting a seller set the stock of **their own** variants
needed an answer Inventory does not hold: ownership is `products.SellerId`, a Catalog column, and
Inventory's rows name only variant ids. So Catalog serves `CatalogOwnership.GetVariantOwners` on the
same gRPC port, and Inventory asks it before `PUT /api/stock/{variantId}` writes anything.

```text
seller ──PUT /api/stock/{variantId} (bearer token)──▶ Inventory
                                                        │ Admin? skip the question entirely
                                                        │ gRPC GetVariantOwners([variantId])
                                                        ├──────────────────────────────▶ Catalog
                                                        │ owner == caller? otherwise 404
                                                        ▼
                                          FOR UPDATE on the stock row, write, announce
```

### Asked live, and deliberately never cached

This codebase's usual answer to "service B needs a fact service A owns" is a read model fed by
events - that is how Catalog knows shop names and availability. It is the **wrong** answer here,
because **authorization must not be eventually consistent**. A copy even seconds behind refuses a
seller her own newly listed product with exactly the 404 that means "not yours", and nothing on
either side can tell that refusal apart from a real one. The read models this project keeps are for
*display*, where stale costs a slightly old page; ownership is a *permission*, where stale costs a
wrong decision.

The same distinction explains why an order line *freezes* its seller at checkout (specs/034) while
stock *asks* who owns a variant now: a sale records who owned it then, like the price; a permission is
about now. Both are right, and neither should be "made consistent" with the other.

### The details that make it correct

- **Catalog answers, it does not decide.** The request carries variant ids, not the caller; the
  response says who owns each one, and `StockOwnership.RequireCanStockAsync` in Inventory compares
  that to `ICurrentUser`. A `MayStock(sellerId, variantId)` method would move an authorization rule
  into the service that does not hold the row being written and would have to be told who is asking.
- **Three 404s, two of which must look identical.** "Not yours" and "no such variant" have the same
  wording, so a seller cannot probe for somebody else's ids. "Yours, but its stock row has not arrived
  from the broker yet" says something else, because only that one is worth retrying.
- **Catalog unreachable is a 503, never a 404.** "I could not find out whether this is yours" is not
  "this is not yours".
- **The question is asked before the transaction**, because a network call inside the `FOR UPDATE`
  would hold a row lock open across a round trip.
- **An administrator never reaches Catalog.** They pass the check anyway, and administering stock
  should not fail while Catalog is down - which is when somebody is most likely to be fixing things.
- **The cost, accepted**: Catalog unreachable means a seller cannot stock. Catalog unreachable also
  means nobody can see the product, so little is lost.

Design and research in [specs/031-seller-stock](../../specs/031-seller-stock/).

---

## What none of this touches: messaging

**Messages are completely unaffected by anything above.**

MassTransit talks to RabbitMQ over **AMQP on TCP 5672**. It does not go through Kestrel, it is not
HTTP/1.1, and it is not HTTP/2. The outbox, the saga, the consumers and the queue bindings are on a
different layer that does not know Kestrel exists.

Changing a service's HTTP protocol changes what `curl` and a gRPC client can do with it, and changes
nothing at all about how events move.

The only HTTP in this system is: each service's REST API, `/health`, and YARP.

---

## What was still to decide in 2026-09-21, and how it was decided

When this document was written the questions below were open. All of them were settled in the
features that followed, and the reasoning is in their `research.md`:

- **REST or gRPC** - gRPC ([specs/009](../../specs/009-catalog-owns-price/)).
- **h2c on a second port, or TLS** - h2c on a second port, as recommended above.
- **Ask at submission, or price the cart**
  ([#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19)) - ask at
  submission, and the cart stores no price at all ([specs/010](../../specs/010-customer-cart/)). A
  priced cart would have moved the call off checkout's critical path without removing it, and raised
  the question of what happens when the price changes between adding and paying; with no stored price
  there is nothing to disagree with.
- **What checkout does when Catalog does not answer** - it refuses with 503 after a bounded retry, and
  never guesses a price.

#18 did not wait for a cart: a live hole that let somebody buy a 40,000,000 item for 1 was fixed first.
