# Data Model: A one-time handoff from the storefront to the back office

## `back_office_handoffs` (Identity) - new

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` | v7 |
| `UserId` | `uuid` | FK `users`, cascade |
| `CodeHash` | `character varying(64)` | SHA-256 hex of the code; unique |
| `ExpiresAt` | `timestamptz` | issued + 30 s |
| `UsedAt` | `timestamptz NULL` | set by the one guarded claim |
| `CreatedAt` | `timestamptz` | |

Migration `AddBackOfficeHandoffs`: a new table, so a rolled-back image ignores it. Rows are kept like reset tokens. The
personal-data inventory declares the table (specs/111): withheld, because it holds a credential's hash. It is erased
with the account (specs/112).

State: issued, then used (once) or expired.
