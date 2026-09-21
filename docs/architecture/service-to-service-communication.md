# How Services Talk to Each Other

**Created**: 2026-09-21 | **Status**: the decision was taken and built — see [specs/009-catalog-owns-price](../../specs/009-catalog-owns-price/)

> **Resolved.** gRPC over h2c on a second port, built in feature 009. What follows describes the
> system *before* that, which is still worth reading because it is why the decision went the way it
> did — and because two of its predictions were tested rather than assumed:
> `Http1AndHttp2` on a plaintext endpoint does **not** serve h2c on .NET 10 (measured, with a
> control), and calling `ListenAnyIP` at all **replaces** `ASPNETCORE_URLS` rather than adding to
> it, which unbound REST the first time it was tried.

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

## What none of this touches: messaging

**Messages are completely unaffected by anything above.**

MassTransit talks to RabbitMQ over **AMQP on TCP 5672**. It does not go through Kestrel, it is not
HTTP/1.1, and it is not HTTP/2. The outbox, the saga, the consumers and the queue bindings are on a
different layer that does not know Kestrel exists.

Changing a service's HTTP protocol changes what `curl` and a gRPC client can do with it, and changes
nothing at all about how events move.

The only HTTP in this system is: each service's REST API, `/health`, and YARP.

---

## Still to decide, and where

These belong in #18's `research.md` when the feature runs, with the rejected alternatives, as the
constitution requires of a recorded decision:

- **REST or gRPC**, given the costs above.
- **h2c on a second port, or TLS**, if gRPC.
- ~~**Ask at submission, or price the cart**~~ — decided in feature 010: ask at submission, and the
  cart stores no price at all. See above.
- *(Original note)* **Ask at submission, or price the cart** ([#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19)).
  A priced cart matches what shops actually do — *the price you saw is the price you pay* — and it
  moves the call off checkout's critical path. It does not remove the need to ask Catalog; it moves
  when. And it raises its own question: what happens when the price changes between adding to the
  cart and paying.
- **What checkout does when Catalog does not answer.** Refusing is almost certainly right for a
  money decision, and "almost certainly" is not the same as written down.

**#18 should not wait for a cart.** A live hole that lets somebody buy a 40,000,000 item for 1 is
not a good reason to build a shopping basket first.
