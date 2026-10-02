# Data Model: Staff roles only in a back-office session

## `refresh_tokens` (Identity) - expand only

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Client` | `character varying(16) NULL` | `Storefront` or `BackOffice`; null (every session from before) reads as `Storefront`. |

Migration `AddSessionClient`: one nullable column. A rolled-back image neither reads nor writes it.

No state transitions: a session keeps its client for life, carried to each successor at rotation.
