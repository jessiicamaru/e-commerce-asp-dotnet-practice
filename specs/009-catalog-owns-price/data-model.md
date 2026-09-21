# Data Model: The Shop Decides What Things Cost

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

No new table and no migration. What changes is **where two fields come from**, and that is a
smaller diff than it is a change of meaning.

---

## The field that moves

```text
BEFORE                                    AFTER

request body                              request body
  productId    ──────► order line           productId ─┐
  productName  ──────► order line                      │
  quantity     ──────► order line           quantity ──┼──► order line
  unitPrice    ──────► order line                      │
                                                       ▼
                                                   Catalog
                                                       │
                                            name, price ──► order line
```

`quantity` still comes from the customer, because it is theirs to choose. `productId` still comes
from the customer, because it is what they are pointing at. **Neither of the other two was ever
theirs.**

---

## What the request may carry

| Field | Before | After | Why |
| :-- | :-- | :-- | :-- |
| `ProductId` | from request | from request | the customer chooses what to buy |
| `Quantity` | from request | from request | the customer chooses how many |
| `ProductName` | **from request** | **removed** | the shop names its own products |
| `UnitPrice` | **from request** | **removed** | the shop prices its own products |

Removed, not ignored (FR-003). The same call the project already made when `UserId` left
`SubmitOrderCommand`: a field the server accepts and discards is an invitation for somebody to start
honouring it again, and it reads as supported in every client that sees it.

---

## The lookup, and its four answers

One call per order, carrying every `ProductId` in it ([research D5](./research.md)).

```text
                    ask Catalog for N products
                                │
        ┌───────────────┬───────┴───────┬────────────────┐
        ▼               ▼               ▼                ▼
   ┌─────────┐   ┌─────────────┐  ┌────────────┐  ┌──────────────┐
   │  priced │   │ no such     │  │ exists but │  │ could not    │
   │         │   │ product     │  │ not for    │  │ ask          │
   │         │   │             │  │ sale       │  │              │
   └────┬────┘   └──────┬──────┘  └─────┬──────┘  └──────┬───────┘
        │               │               │                │
        ▼               ▼               ▼                ▼
   use price       refuse (404)    refuse (409)    retry, then
                                                   refuse (503)
```

**Four, not three-plus-an-error.** "The product does not exist" and "I could not find out whether it
exists" are different facts with different fixes, and a customer told the first when the second is
true will go and check a catalogue that is perfectly fine.

The fourth answer currently has nowhere to go: `Ecommerce.Shared` has only `NotFoundException` and
`ConflictException`, so anything else becomes a **500** — *we are broken* — when the truth is *try
again shortly*. See [research D7](./research.md).

### All or nothing

If any line fails to resolve, the order is refused **entirely**. No order row, no reservation, no
event. One call per order is what makes that structural rather than something the handler has to
remember: there is no point at which two lines are resolved and a third is not.

---

## What is stored, and why it is a copy

```text
order line:
  ProductId     ← the customer's choice
  Quantity      ← the customer's choice
  ProductName   ← Catalog's name,  AT THE MOMENT OF PURCHASE
  UnitPrice     ← Catalog's price, AT THE MOMENT OF PURCHASE
  TotalPrice    ← Quantity × UnitPrice
```

The columns do not change. **What changes is that they become a record rather than a repetition of
the request.**

This is the half of the feature that is easy to get wrong in a way that passes every test written
for the other half. Fetching the correct price and then storing a *reference* to the product —
reading the price through a join at display time — would satisfy every scenario about "the customer
cannot set the price", and would quietly rewrite what somebody paid the next time marketing edits
the catalogue.

An order line is a record of a transaction. Freezing is the point, not an optimisation:

| Later change in Catalog | Effect on an existing order |
| :-- | :-- |
| price changes | **none** |
| product renamed | **none** |
| product deactivated | **none** |
| product deleted | **none** — the order is still readable in full |

That last row is why the name is copied too. An order whose lines cannot be described because the
product is gone is not a record of anything.

---

## What the gRPC response must not contain

`ProductResponse` carries `Availability` — a read model of stock, fed by events from Inventory,
carrying a comment on the record itself explaining that nothing may sell against it.

**The gRPC response deliberately omits it.** This call exists to make a money decision, and putting
a seconds-stale availability flag into its payload is an invitation to make a sell/no-sell decision
from it. Stock is already answered correctly elsewhere: Inventory, under `FOR UPDATE`, at
reservation time.

---

## Entities this feature does not add

No table, no migration, no message contract. Worth saying explicitly: a record in
`Ecommerce.Contracts` would state that a service announces something it does not announce. Nothing
here is published or consumed — this is a question and an answer, not an event.
