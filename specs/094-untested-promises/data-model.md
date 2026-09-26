# Data Model: Behaviour promised but not held by a test

**No table, column, index or migration changes.** This feature adds tests and one validator.

The tests rely on existing guarantees in the database, recorded here because they are what the concurrency tests
exercise:

| Row | Table (service) | Guarantee under test |
| :-- | :-- | :-- |
| 5 | `outgoing_emails` (Identity) | `UPDATE ... SET "Status" = 'Pending' ... WHERE "Id" = @id AND "Status" = 'Failed'` - one row affected once |
| 6 | `shop_applications` (Identity) | partial unique index: one `Pending` application per `UserId` (specs/044) |
| 3 | `orders`, `order_items` (Order) | `TaxRate`, `TaxTotal`, per-line `TaxAmount` stored at checkout and read as stored (specs/012) |
