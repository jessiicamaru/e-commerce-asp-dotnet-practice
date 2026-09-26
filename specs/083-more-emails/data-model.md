# Data Model: Emails for what happens to people

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One column in `ecommerce_identity_db`, one migration. Nothing else in any database changed.

---

## `users` (Identity) - one column added

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Language` | `character varying(8)` | Nullable, no index | The language the person last used the shop in, `vi` or `en`. Null: never said |

Written by:

| When | How |
| :--- | :--- |
| `register`, `register-seller` | Set on the new row from `Accept-Language` (`EmailTemplates.Supported`), null if unsupported |
| `login` | Set on the tracked user, saved with the sign-in, when supported |
| `refresh` | `UPDATE users SET "Language" = @l WHERE "Id" = @id AND ("Language" IS NULL OR "Language" <> @l)`, only when supported and different |

Read by `QueueEmailCommandHandler` when an `EmailRequested` arrives with a blank language.

### Migration `20260926161716_AddUserLanguage`

`AddColumn<string>("Language", "users", "character varying(8)", maxLength: 8, nullable: true)`; `Down` drops it.
Additive only: an earlier Identity image ignores the column. Existing accounts start null and get Vietnamese until
they next sign in or renew.

## `outgoing_emails` (Identity) - unchanged

The eight new templates are new values of `Template` (`varchar(64)`), and `Language` (`varchar(10)`) holds the
resolved language, never the empty string: the queue fills it in before inserting.

## `email_template_versions` (Identity) - unchanged

Administrators' edits of the new templates are stored exactly like the first three (specs/077). No row is written
until somebody edits; the built-in words are the default.

## What each email carries as data

| Template | Data keys | Asked for by |
| :--- | :--- | :--- |
| `ParcelShipped` | `orderId`, `tracking`, `shop` (a seller's parcel only) | Order, `OrderNotices.ShippedAsync` |
| `OrderCancelled` | `orderId`, `total`, `currency` | Order, `OrderNotices.CancelledAsync` |
| `ReturnAccepted` | `orderId` | Order, `ReturnHandlers` |
| `ReturnRefused` | `orderId`, `reason` | Order, `ReturnHandlers` |
| `ReturnRefunded` | `orderId`, `amount`, `currency` | Order, `ReturnHandlers` |
| `SavedBackInStock` | `productId`, `product` | Catalog, `RecordStockAvailabilityCommandHandler` |
| `AccountLocked` | `until` (ISO 8601 UTC), `reason` | Identity, `UserAdministrationHandlers` |
| `AccountBanned` | `reason` | Identity, `UserAdministrationHandlers` |

## Values that changed shape in memory

`ReturnParcel` (Order, Application) gained `Language = ""`, filled by `ReturnRepository` from `orders.Language`.
No column changed for it.
