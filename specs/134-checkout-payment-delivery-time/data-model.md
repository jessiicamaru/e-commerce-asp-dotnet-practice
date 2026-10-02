# Data Model: Checkout says how payment works and how long delivery takes

## `delivery_options` (Order) - expand only

| Column | Type | Notes |
| :-- | :-- | :-- |
| `MinDays` | `integer NULL` | Business days, the soonest. |
| `MaxDays` | `integer NULL` | Business days, the latest. |

CHECK `ck_delivery_options_estimate`: `("MinDays" IS NULL AND "MaxDays" IS NULL) OR ("MinDays" >= 0 AND "MaxDays" <= 60
AND "MinDays" <= "MaxDays")`.

Migration `DeliveryEstimate`: two nullable columns and the CHECK. A rolled-back image neither reads nor writes them and
its inserts leave them null, which the CHECK accepts. Existing rows stay null (no estimate) until an administrator sets
one.

No state transitions.
