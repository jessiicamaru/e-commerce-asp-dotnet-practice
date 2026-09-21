# Phase 0 Research: The Shop Decides What Things Cost

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-21

Nine decisions. Background on the transport is in
[docs/architecture/service-to-service-communication.md](../../docs/architecture/service-to-service-communication.md),
written before this feature; what is here is what that document deliberately left open.

---

## D1 — Ask Catalog at submission, do not keep a copy

**Decision**: Order asks Catalog for the price and name when the order is submitted. It does not
hold a replicated price table.

**Rationale**: The project has already decided this question, for stock, and written the rule down:

> `Product.Availability` is fed by `StockAvailabilityChangedEvent` … **Nothing may sell against
> it**: checkout reserves under `FOR UPDATE` against Inventory's row, and a read model fed by
> messages is seconds behind by design.

*Copy for display; ask the owner for a decision.* Price is the same shape of problem, so it gets the
same answer. A price read model in Order would be seconds behind and would make a money decision —
which is [#4](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/4) happening a
second time, on the number that matters most.

**Alternatives considered**:

- **Order keeps a price read model fed by `ProductPriceChangedEvent`.** No synchronous dependency,
  checkout never blocked by Catalog. Rejected above. It is also the option that looks most
  architecturally tasteful, which is why it is worth naming rather than ignoring.
- **Catalog pushes prices into the order request via the gateway.** Moves the trust boundary to
  the gateway and leaves Order still believing what it is told.

---

## D2 — gRPC, chosen deliberately and not because it is cheaper

**Decision**: gRPC.

**Rationale**: The user asked for it, to practise, and it genuinely fits — internal, frequent, a
tight contract, no browser involved. It is also the **first synchronous call in this system**, which
makes it the right place to learn the shape properly rather than bolting it on later.

**It is the more expensive option here, and that is worth stating plainly.** REST needs twenty lines
and an `HttpClient`; gRPC needs a second port, a new project, code generation and an HTTP/2 story.
Nothing in this feature's requirements is easier because of gRPC. If the goal were closing #18 by
lunchtime, REST wins.

That is a legitimate trade for a learning project with no production and nobody being defrauded —
but a reader a year from now should not have to guess which reason applied.

**Alternatives considered**:

- **REST over the existing port.** Cheapest, and it would have shipped sooner. Rejected for the
  learning goal, not on merit.
- **MassTransit request/response.** Keeps everything on one transport and needs no new port — and
  it makes checkout wait on the broker, which puts a queue in the middle of a synchronous decision.
  Worse than either of the above for this purpose.

---

## D3 — h2c on a second port, no TLS

**Decision**: Catalog keeps 5057 (8080 in-container) for REST, and gains a gRPC port —
**6057 on the host, 8081 in the container** — configured for HTTP/2 only.

**Rationale**: Measured against the running stack, from inside the compose network:

```bash
$ docker run --rm --network server_default curlimages/curl \
    -w 'proto=%{http_version} code=%{http_code}\n' --http1.1 http://catalog:8080/health
proto=1.1 code=200

$ ... --http2-prior-knowledge http://catalog:8080/health
proto=0 code=000          # connection failed
```

**The endpoints serve HTTP/1.1 and nothing else.** A gRPC client gets no connection as things stand.
One plaintext port cannot serve both, because telling the protocols apart needs ALPN and ALPN is
part of the TLS handshake.

h2c rather than TLS because it is the shape a service mesh would produce anyway — application speaks
cleartext HTTP/2, encryption is a sidecar's problem — and because certificates in development, in
containers and in CI are the most painful part of this and teach nothing about gRPC.

**Unverified, and it must be checked during implementation**: Microsoft documents that
`Http1AndHttp2` on a plaintext endpoint still resolves to HTTP/1.1 and gRPC fails. That is why a
*separate* port is specified rather than widening the existing one. Confirm it on .NET 10 rather
than inheriting it — if `Http1AndHttp2` does work, one port would be simpler and this decision
should change.

**Alternatives considered**:

- **TLS on one port.** Correct, standard, and it makes ALPN work so a single port serves both. It
  also means a certificate story for `start-dev`, for seven containers and for CI.
- **Widening the existing port to `Http1AndHttp2`.** Would be the smallest change if it works. See
  above — it is the first thing to test, not the thing to assume.

---

## D4 — The contract gets its own project

**Decision**: a new `Ecommerce.Contracts.Grpc` project holding the `.proto` and its generated code.
Referenced by Catalog's WebApi (server side) and Order's Infrastructure (client side).

**Rationale**: `Ecommerce.Contracts` has **zero package references** — verified, and `CLAUDE.md`
says so as a property worth keeping:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    ...
  </PropertyGroup>
</Project>
```

Adding `Grpc.Tools` and `Google.Protobuf` ends that. The split is also honest rather than merely
convenient: a gRPC contract and a message contract are different kinds of coupling — one
synchronous, one not — and the constitution's rule that *"the only code shared across service
boundaries is `Ecommerce.Contracts`"* is about the **discipline**, not about the file count. A
second, equally pure contracts project keeps the discipline; hiding a transport dependency inside
the existing one would not.

**Alternatives considered**:

- **`.proto` inside `Ecommerce.Contracts`.** One fewer project, and it silently breaks a documented
  property of that one.
- **Duplicate the `.proto` in both services.** No shared project at all, and two files that drift.

---

## D5 — One call per order, not one per line

**Decision**: Order sends every product id in the request and Catalog answers for all of them in one
response.

**Rationale**: This is the question the spec deliberately left to the plan. Per-line calls make
*"refuse the order in full"* awkward — by the time the third line fails, two lookups have already
succeeded and the handler is holding partial state it must remember to discard. One call makes the
decision atomic: either every line resolved or the order is refused, with nothing to unwind.

It is also N round trips against one, on checkout's critical path.

**Alternatives considered**:

- **One call per line.** Simpler contract, and it turns "refuse in full" into a thing the caller has
  to be careful about rather than a thing the shape guarantees.
- **One call per line, in parallel.** Recovers the latency and keeps the partial-state problem.

---

## D6 — Bounded waiting, and a bound that is visible when reached

**Decision**: a 5-second deadline per call, with up to 3 attempts (2 retries) on transient failure,
backing off 200ms then 1s. Retry **only** on `UNAVAILABLE` and `DEADLINE_EXCEEDED`; never on
`NOT_FOUND` or `FAILED_PRECONDITION`.

**Rationale**: FR-010 and FR-011. Catalog is now on checkout's critical path, so a slow Catalog is a
slow checkout for everybody — the wait has to end. Retrying a `NOT_FOUND` is pointless and turns a
clean refusal into three times the latency.

Five seconds is generous for a single indexed lookup on the same network and is chosen so the
timeout fires on something genuinely wrong rather than on a slow afternoon. The elapsed time is
reported on success so the real distribution becomes visible instead of guessed at — the same
reasoning as the saga settle budget in
[007](../007-saga-e2e-verification/research.md).

**Alternatives considered**:

- **No retry.** Simplest, and it lets one dropped connection refuse a customer's order.
- **Unbounded retry.** Turns a Catalog outage into checkout requests that never return, which is
  worse than a refusal because nothing reports it.

---

## D7 — Four outcomes, and one of them has nowhere to go

**Decision**: the lookup has four distinct outcomes, mapped as follows.

| Outcome | gRPC status | What Order does | HTTP the customer sees |
| :-- | :-- | :-- | :-- |
| priced | `OK` | use it | — |
| no such product | `NOT_FOUND` | refuse | **404** via `NotFoundException` |
| exists, not for sale | `FAILED_PRECONDITION` | refuse | **409** via `ConflictException` |
| could not ask | `UNAVAILABLE` / `DEADLINE_EXCEEDED` | refuse, after retries | **503** — and there is nothing to throw |

**The fourth row is a gap this feature creates.** `Ecommerce.Shared/Exceptions/` contains exactly
two types:

```bash
$ ls server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/
ConflictException.cs
NotFoundException.cs
```

and `GlobalExceptionHandler` maps `ValidationException` → 400, `NotFoundException` → 404,
`ConflictException` → 409, `UnauthorizedAccessException` → 401, **everything else → 500**.

So refusing because Catalog is down would answer **500**, which tells the customer *we are broken*
when the truth is *a dependency is down, try again shortly*. A 500 is also the thing monitoring
pages people at 3am about.

**This needs a third exception** — `DependencyUnavailableException` → 503 — in `Ecommerce.Shared`.
That is a change to a shared building block, which is a wider blast radius than the rest of this
feature and is called out here rather than discovered during implementation. `FAILED_PRECONDITION`
mapping to 409 is the loosest fit in the table; 422 would arguably be better and the repository has
no precedent for it.

---

## D8 — The new port needs its own health story

**Decision**: add the standard gRPC health service on the gRPC port. Container health checks keep
probing REST `/health` and are **not** changed.

**Rationale**: gRPC health checking is its own protocol (`grpc.health.v1.Health`) and `curl` cannot
speak it, so the existing `curl -fsS http://localhost:8080/health` check would silently stop meaning
anything if it were pointed at the new port. Leaving the container check alone and adding gRPC
health separately keeps each probe testing the thing it names.

Worth noticing: Catalog will now be *healthy* by its REST probe while its gRPC endpoint could be
broken, and nothing would report it. The end-to-end check catches that — an order would fail — which
is the argument for relying on it rather than adding a second container probe.

---

## D9 — Falsifying the fix

**Decision**: a scenario in `verify-saga.sh` that submits a fabricated unit price and requires the
order to be **refused**, plus a negative control proving that scenario fails against a system that
accepts one.

**Rationale**: FR-012 and SC-005, and the reason is written into the defect itself. Sixty-one tests
pass today and **none of them can see this**, because each supplies its own price and asserts
against the same number. Adding a test shaped the same way would produce the same blindness.

The control is cheap: the current `main` accepts a fabricated price, so the scenario can be run
against `main` and must fail there. That is a negative control that needs no deliberate breakage —
the unfixed system *is* the control, which is rarer than it sounds and should be used while it
lasts.

Once `OrderItemRequest` loses the field (FR-003), a fabricated price can no longer be expressed in
the request at all, so the scenario must send **raw JSON with an extra field** rather than a typed
object — otherwise it tests the type system rather than the server.

---

## Found while reading

**`Ecommerce.Shared` cannot express "a dependency is unavailable".** Recorded under D7. It has not
mattered until now because nothing had a dependency to be unavailable.

**`Product.IsActive` exists** and is exposed on `ProductResponse`, so *"exists but cannot be sold"*
already has a field behind it — FR-007 needs no new catalogue state.

**Catalog's `ProductResponse` carries `Availability`**, a read model of stock, with a comment
explaining that nothing may sell against it. The gRPC contract must **not** repeat that field: this
call exists to make a money decision, and putting a stale availability flag in its response would
invite exactly the misuse that comment warns about.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| `Http1AndHttp2` turns out to work on plaintext | A second port was added for nothing | Tested first (D3); the decision changes if it does |
| Catalog becomes a single point of failure for checkout | An outage stops all ordering, where today orders were merely mispriced | Accepted deliberately; bounded deadline and retry, and the refusal says so rather than returning 500 (D7) |
| The 503 exception is skipped as "polish" | Every dependency failure pages somebody as a 500 | It is a task, not a note, and D7 explains why |
| The new scenario is written like the existing tests | It would supply a price and assert against it — the exact blindness that hid this | D9: raw JSON, and the control is `main` itself |
| gRPC codegen does not reach the image | Builds locally, fails in CI | `.dockerignore` is checked as a task; the existing publish job scans and builds all seven |
