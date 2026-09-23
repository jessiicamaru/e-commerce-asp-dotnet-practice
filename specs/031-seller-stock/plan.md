# Implementation Plan: A seller can stock what they sell

**Branch**: `031-seller-stock` | **Spec**: [spec.md](spec.md) | **Closes**: #70

## Technical Context

A new gRPC contract (`CatalogOwnership`), a service in Catalog to serve it, a client plus
`ICurrentUser` in Inventory, one controller attribute, one ownership check in the handler, and a
quantity control on the seller's product page.

**No migration. No new table. No new message.** The only new coupling is Inventory → Catalog over
h2c, on stock writes only.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **The one under pressure.** Inventory gains a synchronous dependency on Catalog, so Catalog down means a seller cannot stock. Accepted in research D1, for the reason recorded there: Catalog down also means nobody can see the product. The alternative that preserved autonomy — a read model — was rejected for a stronger reason than convenience (D2). |
| II - Clean Architecture | The gRPC client sits behind an Application-layer interface in Inventory, implemented in Infrastructure, the way Order's Catalog client already is. The handler talks to the interface. |
| III - Atomic writes, idempotent messaging | Untouched. The write keeps its transaction, its `FOR UPDATE` lock and its announcement. The ownership call happens **before** the transaction opens - a remote call inside a row lock would hold it open across the network. |
| IV - Identity from the token | Inventory gains `ICurrentUser`, and no endpoint gains a seller id. |
| V - Evidence over assumption | The 403 and the `OutOfStock` in #70 were reproduced before this was written. The two-seller refusal is verified against the API, not the page. |

**Complexity Tracking**: one entry.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| Inventory now calls Catalog synchronously (Principle I) | Ownership lives in `products.SellerId` and the answer must be current | A read model fed by events is the project's usual answer and is wrong here: an authorization answer that is seconds behind refuses a seller their own product with the same 404 that means "not yours", and nothing can tell the two apart (research D2) |

## The two traps this must not fall into

**1. The controller attribute.** specs/027 shipped with its ownership checks unreachable because
the controller still said `Admin`: a real seller was refused at the door, the code deciding whether
the listing was hers never ran, and every unit test passed. This is the same trap in a second
service. The check in Phase 3 must be exercised by a test that goes through the attribute, and by a
real token against the running stack.

**2. Two 404s that look the same on purpose, and one that must not.** "Not yours" and "no such
variant" are deliberately identical. "Your stock row has not arrived yet" is **not** — it is only
reachable after ownership is confirmed, and it must say so, or a seller retrying forever looks the
same as a seller being refused (research D6).

## Phases

**Phase 1 - the contract.** `catalog_ownership.proto`, and `CatalogOwnershipService` in Catalog
answering it. Catalog tests: it reports the seller id, reports empty for the shop's own products,
and omits a variant that does not exist.

**Phase 2 - Inventory learns who is calling.** `AddJwtAuthentication` already registers
`ICurrentUser`; wire the gRPC client and its Application-layer interface.

**Phase 3 - the refusal.** Controller attribute to `"Seller,Admin"`; ownership check in
`SetStockOnHandCommandHandler`, **before** the transaction. Inventory tests: owner passes, other
seller gets 404, administrator passes, missing row after ownership says so.

**Phase 4 - the seller console.** Quantity beside the price editor on `/shop/products/:id`, showing
on hand and reserved, with the server's words on refusal. Vitest tests.

**Phase 5 - end to end.** Two real sellers against the running stack; a full list → stock → buy;
`verify-saga.sh`; Bruno.

**Phase 6 - say so.** CLAUDE.md gains the new gRPC edge and the rule; the service map gains
Inventory → Catalog.

## Verification

- Catalog 111 → ~114, Inventory 31 → ~35, client 33 → ~37.
- **SC-002 against the API with two real tokens.** Not the storefront: a hidden button is not a
  refusal.
- **SC-001 end to end**: list, stock, buy, and watch on-hand fall by exactly one with nothing left
  held — the thing `verify-saga.sh` asserts, done by a seller rather than an administrator.
- `verify-saga.sh` and `verify-auth.sh`, because this touches the service the saga reserves against.
