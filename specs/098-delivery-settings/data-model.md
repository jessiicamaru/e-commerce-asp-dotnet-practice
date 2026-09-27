# Data Model: Administrators manage delivery and the carrier

## Order migration `DeliverySettings` (new tables only)

### `delivery_options`

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Code` | `varchar(32)`, PK | what checkout sends; fixed once created |
| `Name` | `varchar(100)`, not null | shown to shoppers, frozen onto an order at checkout |
| `IsActive` | `boolean` | offered to shoppers |
| `SortOrder` | `integer` | lowest first |
| `CreatedAt`, `UpdatedAt` | `timestamptz` | |

### `delivery_option_prices`

| Column | Type | Notes |
| :-- | :-- | :-- |
| `OptionCode` | `varchar(32)`, FK → `delivery_options` (cascade) | |
| `Currency` | `varchar(3)` | PK with `OptionCode` |
| `Amount` | `decimal(18,2)`, CHECK ≥ 0 | no row: not offered in that currency (specs/022) |

### `carriers`

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `integer`, PK, CHECK `= 1` | one carrier |
| `Name` | `varchar(100)` | |
| `TrackingUrlTemplate` | `varchar(500)`, null | `{reference}` where the reference goes |
| `UpdatedAt` | `timestamptz` | |

Expand only: no existing table changes; orders keep `ShippingOptionCode/Name/Price` as frozen at checkout (specs/011).

## Seeding (every startup)

```sql
INSERT INTO delivery_options ("Code","Name","IsActive","SortOrder","CreatedAt","UpdatedAt")
VALUES (@code, @name, true, @index, now(), now()) ON CONFLICT ("Code") DO NOTHING RETURNING "Code";
-- only for a row just inserted:
INSERT INTO delivery_option_prices ("OptionCode","Currency","Amount") VALUES (...) ON CONFLICT DO NOTHING;
INSERT INTO carriers ("Id","Name","TrackingUrlTemplate","UpdatedAt") VALUES (1, @name, @template, now()) ON CONFLICT ("Id") DO NOTHING;
```

## Audit

`DeliveryOptionSaved` (subject the code) and `CarrierSaved` (subject `1`), category Order, before and after.
