# Implementation Plan: A seller can stock what they sell

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Branch**: `031-seller-stock` | **Spec**: [spec.md](spec.md) | **Closes**: #70

## Summary

Open `PUT /api/stock/{variantId}` to sellers, for their own products only. Inventory cannot answer
"is this yours?" — ownership is `products.SellerId`, a Catalog column — so it asks Catalog **live**
over a new `CatalogOwnership` gRPC contract, before its stock transaction opens, and refuses
somebody else's variant with a 404 worded exactly like a variant that does not exist. An
administrator passes without Catalog being asked. The seller console gains the quantity control on
the product page. Decisions and rejected alternatives are in [research.md](research.md).

## Technical Context

A new gRPC contract (`CatalogOwnership`), a service in Catalog to serve it, a client plus
`ICurrentUser` in Inventory, one controller attribute, one ownership check in the handler, and a
quantity control on the seller's product page.

**No migration. No new table. No new message.** The only new coupling is Inventory → Catalog over
h2c, on stock writes only.

**Language/Version**: C# / .NET 10; TypeScript / React 19 in `client/`

**Primary Dependencies**: `Ecommerce.Contracts.Grpc` (the proto generated `GrpcServices="Both"`),
`Grpc.Net.ClientFactory` 2.66.0 newly referenced by Inventory.Infrastructure, MediatR 12.4.1, MassTransit 8.3.6 (unchanged use), TanStack Query and axios in the client

**Storage**: none new. Reads `products` and `product_variants` in `ecommerce_catalog_db`; writes
`stock_items` in `ecommerce_inventory_db` exactly as before

**Testing**: xUnit against real PostgreSQL — Catalog (`VariantOwnershipTests`, 5433) and Inventory
(`SellerStockTests`, 5437, with a fake `IProductOwnership` in `InventoryTestFixture`); Vitest for
the page; Bruno; `verify-saga.sh` and `verify-auth.sh`

**Target Platform**: Inventory on 5060 (8080 in a container); Catalog gRPC on 5157 under `start-dev`,
`catalog:8081` in compose

**Constraints**: the ownership call must not run inside the `FOR UPDATE` transaction; the refusal must
be a 404 identical to "no such variant"; a Catalog failure must be 503, not 404

**Scale/Scope**: a person setting a number by hand a few times a day. One variant per call, although
the contract is plural

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass, with a justified cost - the one under pressure.** Inventory gains a synchronous dependency on Catalog, so Catalog down means a seller cannot stock. Accepted in research D1, for the reason recorded there: Catalog down also means nobody can see the product. The alternative that preserved autonomy — a read model — was rejected for a stronger reason than convenience (D2). No database is shared: Inventory asks the owner over gRPC and never reads `products`. |
| II - Clean Architecture | **Pass.** The gRPC client sits behind an Application-layer interface in Inventory, implemented in Infrastructure, the way Order's Catalog client already is. The handler talks to the interface. |
| III - Atomic writes, idempotent messaging | **Pass.** Untouched. The write keeps its transaction, its `FOR UPDATE` lock and its announcement. The ownership call happens **before** the transaction opens - a remote call inside a row lock would hold it open across the network. |
| IV - Identity from the token | **Pass.** Inventory gains `ICurrentUser`, and no endpoint gains a seller id. |
| V - Evidence over assumption | **Pass.** The 403 and the `OutOfStock` in #70 were reproduced before this was written. The two-seller refusal is verified against the API, not the page. |

**Post-design re-check**: unchanged. The one entry below is the Principle I cost, justified; no other
principle is touched. Principle III admits no entry and needs none - the stock write is the same
transaction it was.

## Project Structure

### Documentation (this feature)

```text
specs/031-seller-stock/
├── spec.md
├── plan.md                   # this file
├── research.md               # D1-D8
├── data-model.md             # no schema change; what is read and written
├── quickstart.md             # validation scenarios
├── contracts/
│   └── api.md                # the REST change and the CatalogOwnership proto
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/
├── Ecommerce.Contracts.Grpc.csproj                      # registers the proto
└── Protos/catalog_ownership.proto                       # new
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs   # GetVariantOwnersAsync, VariantOwnership
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs
└── Ecommerce.Catalog.WebApi/
    ├── Grpc/CatalogOwnershipService.cs                  # new
    └── Program.cs                                       # MapGrpcService
server/src/Services/Inventory/
├── Ecommerce.Inventory.Application/
│   ├── Common/Interfaces/IProductOwnership.cs           # new
│   ├── Common/StockOwnership.cs                         # new: the one place that decides
│   └── Stock/Commands/SetStockOnHand/SetStockOnHandCommandHandler.cs
├── Ecommerce.Inventory.Infrastructure/
│   ├── Catalog/GrpcProductOwnership.cs                  # new
│   ├── DependencyInjection.cs                           # gRPC client, Catalog:GrpcAddress / CATALOG_GRPC_ADDRESS
│   └── Ecommerce.Inventory.Infrastructure.csproj
└── Ecommerce.Inventory.WebApi/Controllers/StockController.cs   # "Seller,Admin"
server/docker-compose.app.yml                            # CATALOG_GRPC_ADDRESS, depends_on catalog
server/tests/Ecommerce.Catalog.Tests/VariantOwnershipTests.cs    # new, 4 tests
server/tests/Ecommerce.Inventory.Tests/SellerStockTests.cs       # new, 7 tests
server/tests/Ecommerce.Inventory.Tests/InventoryTestFixture.cs
client/src/services/stock/{index.ts,types.ts}            # new
client/src/hooks/stock/index.ts                          # new
client/src/pages/shop-product/{index.tsx,index.test.tsx}
client/src/locales/{en,vi}/seller.json
bruno/seller/a seller stocks their own product.yml       # new
bruno/seller/a customer cannot stock it.yml              # new
CLAUDE.md
```

## Complexity Tracking

One entry.

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

**What the pull request recorded** (#71): Catalog 111 → **115**, Inventory 31 → **38**, client
33 → **38**; Bruno 93/93 requests and 145/145 tests; `verify-saga.sh` and `verify-auth.sh` passed. The
two-seller refusal was run against the running stack with real tokens, and the racing case (the stock
row not arrived yet) genuinely occurred on that run.

## What this feature does not finish

- Stocking is still a second step after listing; the create form has no quantity (research D8).
- Catalog unreachable means a seller cannot stock; there is no fallback, deliberately (D1, D2).
- Only `PUT /api/stock/{id}` asks about ownership. Reading stock stays anonymous (D7).
