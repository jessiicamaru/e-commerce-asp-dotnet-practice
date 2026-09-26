# Message Contracts: The Shop Is a Marketplace

> Written on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md) D1, D6

Two new integration events in `Ecommerce.Contracts/Identity/SellerEvents.cs`. They are the first messages
Identity has ever published.

---

## `SellerRegisteredEvent`

```csharp
public record SellerRegisteredEvent(Guid SellerId, string ShopName, DateTime RegisteredAt);
```

**Publisher**: Identity's `RegisterSellerCommandHandler`. The user (with `Seller` and `Customer`), the
`seller_profiles` row and the event are staged, published through Identity's EF outbox
(`AddEntityFrameworkOutbox<ApplicationDbContext>` + `UseBusOutbox()`), then saved **once** - so a registered
seller Catalog is never told about cannot exist. The refresh token is saved in a second, later save that
carries no message.

**Consumer**: Catalog's `SellerRegisteredConsumer` → `RecordSellerCommand(SellerId, ShopName, RegisteredAt)`.

---

## `SellerRenamedEvent`

```csharp
public record SellerRenamedEvent(Guid SellerId, string ShopName, DateTime RenamedAt);
```

**Publisher**: Identity's `RenameShopCommandHandler` (`PUT /api/sellers/me/shop-name`) - the profile update and
the event in one save, the same way.

**Consumer**: Catalog's `SellerRenamedConsumer` → the same `RecordSellerCommand(SellerId, ShopName, RenamedAt)`.

---

## Idempotency and ordering

Both consumers funnel into `SellerRepository.TryRecordAsync`, which is guarded on the **timestamp**:

```sql
UPDATE sellers SET "ShopName" = @name, "ObservedAt" = @at
WHERE "SellerId" = @id AND "ObservedAt" < @at;
-- no row updated and none exists -> INSERT; no row updated but one exists -> nothing (redelivery or overtaken)
```

| Delivery | Result |
| :--- | :--- |
| The same event twice | Second is a no-op: `ObservedAt` is not older |
| A rename overtaken by a newer one | Loses: comparing names instead of times would let it win (`An_overtaken_rename_does_not_win`) |
| A rename before its registration | Inserts the renamed shop; the late registration, being older, changes nothing |
| Two first deliveries at once | The `sellers` primary key refuses the second insert |

No product row is written by either consumer; names are joined in at read time
(`Renaming_a_shop_changes_what_its_products_say_without_writing_one`).

---

## Queue names

Catalog's consumers are named for their events, so they do not collide with anything existing. Identity
registers an endpoint-name prefix `IdentitySvc` although it consumes nothing yet, so that its first consumer
cannot share a queue with another service's (the CLAUDE.md gotcha).

## Broker down

Identity keeps working without RabbitMQ: the publish is an outbox row written inside the request's
transaction. Verified on the stack at the merge - registration 200 with the broker stopped, the outbox
drained to 0 and Catalog received the shop once it returned.
