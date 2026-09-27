# Message contract: Shoppers report a review, a question or a product

This feature adds no new integration events. It publishes existing message types through Catalog's outbox:

- **`UserNotificationRequested`** with two new kinds, both declared in `notification-kinds.json`:
  - `ReportActioned`, data `{ "product" }`, link `/products/{productId}`, sent to each reporter when the thing is hidden
    or taken down.
  - `ReportDismissed`, data `{ "product" }`, sent to each reporter when staff dismiss.
- **`AuditEntryRecorded`**, category `Moderation`, action `ReportsDismissed`, subject the target.

Both are staged inside the transaction that closed the reports: the hide's stage callback, or the dismiss transaction.
