# Data model: A seller's list of the returns of their parcels

**No schema change.** Everything read already exists.

| Table | Column / index | Used for |
| :-- | :-- | :-- |
| `parcel_returns` | `SellerId`, `IX_parcel_returns_SellerId` | The seller's list (the filter). |
| `parcel_returns` | `Status`, `UpdatedAt` | The tab, and the queue order. |
| `parcel_returns` | `OrderId` | The badge's subquery from the sales page. |

## Response change

`SaleSummaryResponse` gains an optional last parameter, `string? ReturnStatus = null`. It is null when the caller's
parcel of that order has no return. Adding it is backward compatible for any reader.

No state transitions change: the return's steps are specs/066's.
