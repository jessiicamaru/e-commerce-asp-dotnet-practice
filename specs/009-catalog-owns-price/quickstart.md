# Quickstart & Validation: The Shop Decides What Things Cost

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

Nine scenarios. **Scenario 1 is the defect itself**, run again — it currently succeeds and must
stop.

## The control is free, and it expires

`main` today accepts a fabricated price. That makes it a **negative control that needs no deliberate
breakage**: scenario 1 can be run against `main` and must fail there, and against this branch and
must pass. That is rarer than it sounds — use it while it lasts, because after this merges the
control costs a scratch branch.

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

Six services, and now a seventh port. Confirm both of Catalog's surfaces answer before anything
else:

```bash
curl -s -o /dev/null -w 'REST  proto=%{http_version} code=%{http_code}\n' http://localhost:5057/health
grpcurl -plaintext localhost:6057 list
```

---

## Scenario 1 — A fabricated price is refused *(US1, FR-001, SC-001)*

**This is the reproduction from [#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18), and it must now come out the other way.**

Create a product listed at 40,000,000 with stock, then order it claiming it costs 1 — sending **raw
JSON with the extra field**, not a typed object:

```bash
curl -s -X POST http://localhost:5059/api/orders \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $CUSTOMER" \
  -d '{"items":[{"productId":"'$PID'","productName":"Free Laptop lol","quantity":1,"unitPrice":1}]}'
```

**Expect**: the order is **not** created at 1. Either it is refused, or it is created at 40,000,000.
Stock is unchanged if refused.

**On `main` this produces**:

```text
order total    1
item name      Free Laptop lol
final status   Completed after 2s
stock after:   4              ← gone, sold for 1
```

**Raw JSON matters.** Once the field is gone from the request type, sending a typed object would
test the type system rather than the server. The server must ignore or reject an extra field that a
determined caller can always put on the wire.

---

## Scenario 2 — The price with no claim at all *(FR-001, SC-002)*

The same order, sending only `productId` and `quantity`.

**Expect**: created at **exactly** the catalogue price. Compare against
`GET /api/products/{id}` — zero tolerance.

This is the scenario that proves the lookup works, as opposed to proving that refusal works.
Scenario 1 passes if the system simply rejects everything.

---

## Scenario 3 — The name is the shop's name *(FR-002)*

Read the order back.

**Expect**: the product's catalogue name. Not `Free Laptop lol`, and not empty.

---

## Scenario 4 — What was bought does not change *(US2, FR-005, SC-003)*

1. Place an order. Record its line price, total, and product name.
2. Change the product's price in Catalog, and rename it.
3. Read the order back.

**Expect**: line price, total and name **all unchanged**.

**This is the half that passes every test written for scenario 1 and is still wrong.** Fetching the
right price and storing a *reference* — resolving it through a join at read time — satisfies
scenarios 1–3 completely, and silently rewrites what somebody paid the next time the catalogue is
edited.

**Also confirm** the order is still readable in full after the product is deactivated, and after it
is deleted.

---

## Scenario 5 — An unknown product is refused, in full *(FR-006, SC-004)*

| Order | Expect |
| :-- | :-- |
| one line, a product id that does not exist | refused, **404** |
| two lines, first real and second not | refused **entirely** — no order row, no reservation |

**Failure looks like**: an order containing only the first line. Partial acceptance is worse than
refusal, because nobody asked for half of it.

---

## Scenario 6 — Not for sale is not the same as not there *(FR-007)*

Deactivate a product in Catalog, then order it.

**Expect**: refused with **409**, distinguishable from scenario 5's 404.

The distinction is the point. A customer told "no such product" about something they are looking at
goes and checks a catalogue that is fine.

---

## Scenario 7 — NEGATIVE CONTROL: Catalog unreachable refuses, and says which *(FR-009, FR-010, SC-007)*

**Do not skip this.** It is the branch that is easiest to leave out and the one whose absence turns
the feature into theatre — a lookup that falls back to the request on failure is worse than no
lookup, because it looks safe.

```bash
docker stop ecommerce-catalog
# submit an order
```

**Expect**:

- The order is **refused**. Zero orders created.
- The response is **503**, not 500 and not 404.
- The message names the **lookup**, not the product.
- It gave up after the documented attempts rather than hanging.

**Failure looks like** any of: an order created anyway; a 404 (a customer sent to check a catalogue
that is merely offline); a 500 (*we are broken*, which pages somebody); or a request that never
returns.

---

## Scenario 8 — The request has no price field at all *(FR-003, SC-006)*

Inspect `SubmitOrderCommand` / `OrderItemRequest`.

**Expect**: no `UnitPrice`, no `ProductName`. **Removed, not ignored.**

A field the server accepts and discards reads as supported in every client that sees it, and is an
invitation for somebody to start honouring it again. This is the same call already made when
`UserId` left this command.

---

## Scenario 9 — The check would catch this happening again *(US3, FR-012, SC-005)*

`verify-saga.sh` gains scenario 1 as an assertion.

**Both directions, and both recorded**:

| Run against | Expect |
| :-- | :-- |
| this branch | **passes** |
| `main` (the unfixed system) | **fails**, naming the fabricated price |

**A check that is red either way proves nothing, and neither does one that has never been red.**
Sixty-one tests pass today and not one of them can see this defect, because each supplies its own
price and asserts against that same number — they verify arithmetic, not authority. A new test
shaped the same way would inherit the same blindness.

---

## Measure, do not assume *(SC-008)*

Checkout now waits on Catalog. Record it:

| | time to order accepted |
| :-- | :-- |
| before (on `main`) | _fill_ |
| after | _fill_ |

The lookup is one round trip on the same network and should be invisible. **Say so with a number
rather than assuming it** — if it is not invisible, the priced-cart shape
([#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19)) is the answer and
this is the evidence for it.

---

## What passing all nine does not prove

- **That the customer sees the price they were shown.** Nothing shows them a price beforehand —
  there is no cart. They are charged the price at submission, which is correct today and expires the
  day a quote exists.
- **That the total is right.** It is the sum of the lines. Shipping, tax and discounts are #21 and
  are not here.
- **That gRPC is the right choice.** It proves the guarantee holds over gRPC. REST would have given
  the same guarantee for less; the choice was made to practise, and
  [research D2](./research.md) records that plainly.
- **That the system survives Catalog being slow rather than absent.** Scenario 7 stops it. A Catalog
  answering in four seconds every time is a different and unmeasured problem.
