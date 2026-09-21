# Phase 0 Research: Somewhere to Put What You Intend to Buy

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-21

Seven decisions. The two largest — a separate service, no guest carts — were asked and answered
before this document existed; see the spec.

---

## D1 — Remove ordered lines when the order COMPLETES, not when it is submitted

**Decision**: the cart loses the ordered lines on `OrderCompletedEvent`. On `OrderFailedEvent` the
cart is left alone.

**Rationale**: the saga's `Failed` is **terminal** — there is no "retry payment" in this system.
Removing lines at submission means a declined payment leaves the customer with an empty cart *and* a
dead order, and they must rebuild the cart by hand to try again. Removing them at completion means a
declined payment leaves the cart exactly as it was and the customer simply checks out again.

That also gives the spec's FR-006 — *"removed only after the order has been accepted"* — its most
useful reading: accepted means it went through.

**Alternatives considered**:

- **Remove at submission.** Far simpler: `OrderSubmittedEvent` carries everything needed. Rejected
  for the declined-payment case above, which is the common failure in a real shop.
- **Remove at submission and restore on failure.** Two writes where one would do, and a window in
  which the cart is wrong in the direction a customer notices.

---

## D2 — The completion signal carries nothing, so the cart remembers

**Decision**: the cart consumes **three** events and keeps its own record of each checkout:

| Event | What it carries | What the cart does |
| :-- | :-- | :-- |
| `OrderSubmittedEvent` | order, **user, items** | records what this order will remove |
| `OrderCompletedEvent` | order **only** | removes those lines |
| `OrderFailedEvent` | order **only** | forgets the record; cart untouched |

**Rationale**: measured, not assumed — the contracts say:

```csharp
public record OrderSubmittedEvent(Guid OrderId, Guid UserId, decimal TotalAmount,
                                  List<OrderItemDto> Items, DateTime CreatedAt);
public record OrderCompletedEvent(Guid OrderId, DateTime CompletedAt);
public record OrderFailedEvent(Guid OrderId, string Reason, DateTime FailedAt);
```

`OrderCompletedEvent` has **no items and no user**. The cart cannot know what to remove from it
alone, so it keeps what it learned from the submission and applies it on completion. A service
building the view it needs from events it already receives is the idiomatic answer in an
event-driven system, and it changes **no contract**.

**Alternatives considered**:

- **Add items and user to `OrderCompletedEvent`.** A contract change consumed by three services —
  `risk: breaking` — and the saga that publishes it does not hold the items, so it would need to
  start storing them. Widens two services to spare one.
- **Ask Order what was in the order, when completion arrives.** A synchronous call from inside a
  message consumer, which turns an asynchronous path into one that fails when Order is down.

---

## D3 — The two events can arrive in either order, and both orders must work

**Decision**: one row per order in `checkout_outcomes`, and **whichever event arrives second applies
the removal**. An `applied` flag makes it happen once.

```text
Submitted first:  record items ─────────────► Completed: items present -> apply
Completed first:  mark completed (no items) ─► Submitted: already completed -> apply
Either, twice:    applied = true ──────────► no-op
```

**Rationale**: the two events are different message types on different exchanges, and nothing
orders delivery across them. This is not a hypothetical in this repository: issue
[#15](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/15) was exactly this —
on a cold start the saga received a reply before the event it replied to had been committed, and
four orders stranded silently for eighteen days.

Designed for only the likely order, the cart would do nothing when completion arrived first, then
record items from the late submission that nothing would ever apply. **The cart would never clear,
and nothing would say so** — the same silent shape as #15.

**Idempotency** is a guarded state transition, as the constitution requires rather than a
configuration flag: the row is taken `FOR UPDATE`, and the removal happens only while `applied` is
false, in the same transaction that sets it.

---

## D4 — Order reads the cart over gRPC, and forwards the customer's token to do it

**Decision**: at checkout Order asks Cart for **the caller's** cart over gRPC, and sends the
customer's bearer token in the call's metadata. Cart validates it and reads the user from it. The
request carries **no user id**.

**Rationale**: FR-005 — checkout is built from the cart, not from a list the client supplies — and
the cleanest way to know *whose* cart without weakening Principle IV.

The tempting shortcut is `GetCart(user_id)`. It would let anything on the network read anybody's cart
by naming them, and it is the `UserId`-in-the-body defect again, one hop further in. Forwarding the
token instead means **Cart identifies the customer the same way every other service does** —
`ICurrentUser`, from a validated token — and the internal call adds no new way to be somebody else.
Token propagation is the standard pattern for exactly this, and it costs one header.

It reuses the shape feature 009 built: `ICartReader` declared in `Order.Application`, the gRPC client
in `Order.Infrastructure`, h2c on a second port.

**The cost, stated**: checkout now depends synchronously on **Catalog and Cart**. Either being down
refuses orders with 503. The first synchronous call was a deliberate trade; this is the second, made
knowing that.

**Alternatives considered**:

- **`GetCart(user_id)`.** Rejected above.
- **The client reads its cart and submits the items to Order.** No new synchronous dependency, and
  functionally close — the removal is event-driven either way. Rejected because it leaves two ways
  to place an order and makes FR-005 false as written: the order would be whatever the client sent,
  not what the cart held.
- **Checkout as a Cart command that calls Order.** Moves the ordering flow into the cart service,
  where it does not belong; Order owns orders.

---

## D5 — The cart's price is for display, and says so

**Decision**: a cart line shows the shop's **current** price, fetched from Catalog when the cart is
read. Nothing the cart holds is used to charge. Checkout re-prices through the path feature 009
built, and the response reports the total that was actually charged.

**Rationale**: the constitution settles it outright:

> Where a value is duplicated elsewhere for display, the non-owning copy MUST NOT inform any decision
> — most importantly, a sell/no-sell, allow/deny, or **charge/refuse** decision.

A cart is a new place a price can live. Feature 009 closed the hole where the customer set the price;
a cart that remembered a price and charged it would reopen it, through a copy the system made itself.
So the cart **stores no price at all** — it asks Catalog when it is read — and there is nothing
stale to charge by accident.

**Alternatives considered**:

- **Freeze the price when an item is added, and charge it.** *"The price you saw is the price you
  pay"* is a real and good promise — and it needs a quote with an expiry, or a customer holds a low
  price for ever. That is a quoting feature, out of scope, and would breach the rule above as it
  stands.
- **Store the price at add time for display only.** Honest if labelled, and it goes stale silently;
  fetching it on read is cheaper than keeping it right.

---

## D6 — Two endpoints, declared together

**Decision**: Cart serves REST on 8080 and gRPC on 8081 in-container (host **5062** and **6062**),
both declared explicitly in `ConfigureKestrel`, with no `app.Run(url)` fallback.

**Rationale**: learned the hard way in feature 009. Calling `ListenAnyIP` at all **replaces**
`ASPNETCORE_URLS` rather than adding to it; configuring only the gRPC endpoint silently unbound REST
and the container went unhealthy. The same code shape is copied here on purpose.

---

## D7 — A fifth consumer of the order events, named so it cannot collide

**Decision**: Cart calls `SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "CartSvc", ...))`.

**Rationale**: a consumer's **class name becomes its queue name**. Inventory and Order once both
declared `OrderCompletedConsumer`, bound to the same queue, and competed for the event — the order
settled and the stock stayed held, with every unit test green. Cart would be the third service with a
consumer of that name. The prefix is what keeps each service its own copy of the event.

`verify-saga.sh` is the check that catches it if this is ever lost: the stock assertion goes red, and
it did exactly that in feature 007's acceptance control.

---

## D8 — Showing a cart and charging for one are different questions

**Decision**: Catalog gains a second RPC, `DescribeProducts`, which answers **per product** and never
fails the whole call because one product is missing. Checkout keeps using `GetPrices`, which stays
all-or-nothing.

**Rationale**: found while designing D5, not before. `GetPrices` refuses the **entire** request with
`NOT_FOUND` if any single id is unknown — deliberately, because feature 009 needed checkout to be
all-or-nothing. Reused for displaying a cart, that property becomes a bug: a cart holding one
deleted product could not show the price of anything else in it.

FR-011 wants the opposite for display: the missing line stays visible and **marked**, and the rest of
the cart carries on. So the two callers are asking two different questions:

| Caller | Question | RPC | On a missing product |
| :-- | :-- | :-- | :-- |
| Order, at checkout | *May this be sold, at what price?* | `GetPrices` | refuse everything |
| Cart, on read | *What do these look like right now?* | `DescribeProducts` | report it missing, answer the rest |

Adding an RPC is **additive** — no existing message or method changes, so nothing already deployed
breaks.

**Also**: if Catalog is unreachable when a cart is read, the cart still returns its lines — product
and quantity are the cart's own — with prices marked unavailable. A customer can see what they chose
even when they cannot see what it costs; refusing to show the cart at all would be the worse failure.
Checkout, by contrast, still refuses with 503, because there the price is not optional.

**Alternatives considered**:

- **Reuse `GetPrices` and parse the missing ids out of the error message.** Parsing a human-readable
  string as a contract.
- **Call `GetPrices` once per product.** N round trips on every cart view, to route around a property
  that is correct for its real caller.
- **Make `GetPrices` partial.** Weakens the one guarantee feature 009 exists to provide.

## Found while reading

- **`OrderCompletedEvent` carries no items** — D2's whole premise, and not visible without reading
  the contract.
- **Every service would gain a fifth dependency to name in CI.** The `saga-e2e` job starts six
  services; with checkout reading the cart it needs Cart and its database too, and so does
  `auth-smoke` — which submits an order.
- **Both end-to-end scripts submit orders from a list.** Once checkout reads the cart they must fill a
  cart first, which is the more realistic flow and makes those scripts cover the cart as well.

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Completion arrives before submission and nothing applies | Cart never clears, silently | D3: whichever arrives second applies |
| A redelivered completion removes lines twice | Lines added later vanish | D3: `applied` flag under `FOR UPDATE` |
| The queue-name collision returns | One service misses the event | D7 prefix; `verify-saga.sh` detects it |
| Cart becomes a second synchronous dependency of checkout | Cart down refuses orders | Accepted in D4; 503 via the existing `DependencyUnavailableException` |
| `ListenAnyIP` unbinds REST again | Cart unhealthy on first container build | D6: both endpoints declared together, copied from Catalog |
| A new project is missing from the Dockerfile | Build fails at publish | Four `COPY` lines added with the projects, not after |
