# Messages

> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) from commit `6149cdc`. Do not edit by hand - change the code and run the script again.

Every integration message in `Ecommerce.Contracts` - the only coupling between services - with who publishes it and who consumes it. Every publish goes through the publisher's transactional outbox, and every consumer is idempotent (see [reliable messaging](../architecture/reliable-messaging-and-outbox-pattern.md)). A consumer's class name is its queue name, so two services never share one.

**24 messages.**

| Message | Owner | Published by | Consumed by |
| :-- | :-- | :-- | :-- |
| `AuditEntryRecorded` | Activity | any service, through `Ecommerce.Shared` | Activity (`RecordAuditEntryConsumer`) |
| `UserNotificationRequested` | Activity | any service, through `Ecommerce.Shared` | Activity (`RecordNotificationConsumer`) |
| `ProductCreatedEvent` | Catalog | Catalog | Inventory (`ProductCreatedConsumer`) |
| `ProductDeletedEvent` | Catalog | Catalog | Inventory (`ProductDeletedConsumer`) |
| `ProductVariantCreatedEvent` | Catalog | Catalog | Inventory (`ProductVariantCreatedConsumer`) |
| `AccessTokensRevoked` | Identity | Identity | Activity (`AccessTokensRevokedConsumer`, Shared), Cart (`AccessTokensRevokedConsumer`, Shared), Catalog (`AccessTokensRevokedConsumer`, Shared), Identity (`AccessTokensRevokedConsumer`, Shared), Inventory (`AccessTokensRevokedConsumer`, Shared), Order (`AccessTokensRevokedConsumer`, Shared), Payment (`AccessTokensRevokedConsumer`, Shared) |
| `EmailRequested` | Identity | any service, through `Ecommerce.Shared` | Identity (`QueueEmailConsumer`) |
| `SellerRegisteredEvent` | Identity | Identity | Catalog (`SellerRegisteredConsumer`) |
| `SellerRenamedEvent` | Identity | Identity | Catalog (`SellerRenamedConsumer`) |
| `InventoryReservationFailedEvent` | Inventory | Inventory | Orchestrator (saga) |
| `InventoryReservedEvent` | Inventory | Inventory | Orchestrator (saga) |
| `ReleaseInventoryCommand` | Inventory | Orchestrator | Inventory (`ReleaseInventoryConsumer`) |
| `ReserveInventoryCommand` | Inventory | Orchestrator | Inventory (`ReserveInventoryConsumer`) |
| `StockAvailabilityChangedEvent` | Inventory | Inventory | Catalog (`StockAvailabilityChangedConsumer`) |
| `OrderCancelledEvent` | Order | Order | Inventory (`RestockCancelledOrderConsumer`), Payment (`RefundCancelledOrderConsumer`) |
| `OrderCompletedEvent` | Order | Orchestrator | Cart (`OrderCompletedConsumer`), Inventory (`OrderCompletedConsumer`), Order (`OrderCompletedConsumer`) |
| `OrderFailedEvent` | Order | Orchestrator | Cart (`OrderFailedConsumer`), Order (`OrderFailedConsumer`) |
| `OrderSubmittedEvent` | Order | Order | Cart (`OrderSubmittedConsumer`), Orchestrator (saga) |
| `ParcelDeliveredEvent` | Order | Order | Catalog (`ReviewEligibilityConsumer`) |
| `ParcelReturnedEvent` | Order | Order | Inventory (`RestockReturnedParcelConsumer`), Payment (`RefundReturnedParcelConsumer`) |
| `PaymentFailedEvent` | Payment | Payment | Orchestrator (saga) |
| `PaymentProcessedEvent` | Payment | Payment | Orchestrator (saga) |
| `ProcessPaymentCommand` | Payment | Orchestrator | Payment (`ProcessPaymentConsumer`) |
| `RefundPaymentCommand` | Payment | Orchestrator | Payment (`RefundLatePaymentConsumer`) |
