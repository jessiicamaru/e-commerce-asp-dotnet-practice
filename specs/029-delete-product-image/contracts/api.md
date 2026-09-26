# Contracts: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

## No contract changes

No endpoint, request, response or message record changes shape.

| Endpoint | Before | After |
| :-- | :-- | :-- |
| `DELETE /api/products/{id}` | 204; row gone, variants gone, `ProductDeletedEvent` published, **image file left on the volume** | 204; row gone, variants gone, same event published, **image file deleted** |

`ProductDeletedEvent` is untouched - same fields, same publisher, same consumers. Inventory keeps
dropping stock rows exactly as it does now.

## The observable difference

```text
PUT    /api/products/{id}/image     -> 200
DELETE /api/products/{id}           -> 204
# before: one file remains on the volume, unreachable, forever
# after:  the store holds nothing for that product
```

## What a store failure looks like

Unchanged from the caller's side, which is the point:

```text
DELETE /api/products/{id}   -> 204      # even when the store cannot delete
```

and a warning in the log naming the key, worded as `RemoveProductImage` already words it.

## Messages and gRPC

None changed. `ProductDeletedEvent` is still published through the outbox in the deletion's own
transaction ([specs/024 messages](../../024-delete-product/contracts/messages.md)); the file delete happens
after that transaction and publishes nothing.
