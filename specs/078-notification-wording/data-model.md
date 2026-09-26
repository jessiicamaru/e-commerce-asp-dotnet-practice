# Phase 1 Data Model: An administrator rewords the notifications

> Written on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in Activity's database (`ecommerce_activity_db`, host port 5440), one new section in the shared
`notification-kinds.json`, and nothing changed anywhere else. The storefront's bundled words
(`client/src/locales/{en,vi}/notifications.json`) are unchanged: they are the default the table's rows are laid over.

---

## `notification_wording_versions` (Activity)

Entity `NotificationWordingVersion` (`Ecommerce.Activity.Domain/Entities`), configured in
`NotificationWordingVersionConfiguration`, created by migration **`20260926112005_AddNotificationWordingVersions`**.

One row per saved version of one key's words in one language. Append-only: nothing updates or deletes a row. The
current words of a key and language are the row with the highest `Version`.

| Column | Type (PostgreSQL) | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | PK. `Guid.CreateVersion7()`, `ValueGeneratedNever()` |
| `Key` | `character varying(80)` | no | A declared kind (`OrderPaid`) or a kind with an i18next plural suffix (`NewReview_one`) - `zero`, `one`, `two`, `few`, `many`, `other` |
| `Language` | `character varying(10)` | no | `vi` or `en` (`NotificationWording.Languages`) |
| `Version` | `integer` | no | 1, 2, 3 ... per `(Key, Language)`; the editor's `expectedVersion` + 1 |
| `IsDefault` | `boolean` | no | True: back to the storefront's bundled words (a reset, or a restored reset) |
| `Text` | `character varying(1000)` | yes | The sanitised words with `{{placeholders}}`; null exactly when `IsDefault` |
| `CreatedAt` | `timestamp with time zone` | no | When this version was saved (UTC) |
| `CreatedBy` | `uuid` | no | The administrator, from `ICurrentUser` - never from the body |

**Constraints and indexes**:

- `PK_notification_wording_versions` on `Id`.
- **`IX_notification_wording_versions_Key_Language_Version`, unique** on `(Key, Language, Version)`. This is what
  decides between two saves on top of the same version: both ask for N + 1, `INSERT ... ON CONFLICT ("Key",
  "Language", "Version") DO NOTHING` lets one of them have it, and the other affects zero rows and is answered 409
  (research D5). It is also the lookup path for the current version and the history.
- **`CK_notification_wording_versions_text`**: `"IsDefault" OR "Text" IS NOT NULL` - an edit always has words.

**Lengths**: the words are at most **500** characters (`NotificationWording.MaxLength`), checked on the request and
again after sanitising. The column allows 1000; why the column is wider than the rule is not recorded.

**No foreign keys.** `Key` names a kind declared in `notification-kinds.json`, which is a file, not a table;
`CreatedBy` is an Identity user id, and Activity reads no other service's database (Principle I).

### What a row means over time

```text
 (no row)  ──save──▶  v1 edit  ──save──▶  v2 edit  ──reset──▶  v3 default  ──restore v2──▶  v4 edit (v2's words)
   bundle              edit                 edit                  bundle                        edit
```

| Action | Row added | Refused when |
| :-- | :-- | :-- |
| Save | `IsDefault = false`, `Text` = the sanitised words | Unknown key or language (404); placeholder the kind cannot fill, bad link, empty or over 500 characters (400); `expectedVersion` not the current version, or the number taken meanwhile (409) |
| Reset | `IsDefault = true`, `Text = null` | Unknown key or language (404); no version yet or already default (409 "These words are already the storefront's own."); stale (409) |
| Restore v*n* | A copy of v*n* - its words sanitised and checked **again** with today's rules, or a default if v*n* was one | Unknown key, language or version (404); fails today's checks (400); stale (409) |

Every added row has exactly one `AuditEntryRecorded` staged in the same transaction (below); a refused one has none.

### What the public read returns

`GET /api/notifications/wording` takes the current row of every `(Key, Language)` that has one, drops those with
`IsDefault`, and returns `{ "vi": { key: text }, "en": { key: text } }` - both languages always present, possibly
empty. A key never edited, or reset, is absent, and the storefront shows its bundled words.

---

## `notification-kinds.json`: the `placeholders` section (Ecommerce.Shared)

`server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`, embedded in `Ecommerce.Shared` and
read by the storefront's tests too (specs/048). The new top-level `placeholders` object maps each `{{placeholder}}` a
notice's words may use to the data keys it is made from (`$comment` aside):

| Placeholder | Made from the data keys |
| :-- | :-- |
| `order` | `orderId` |
| `total` | `total`, `currency` |
| `amount` | `amount`, `currency` |
| `tracking` | `tracking` |
| `shop` | `shop` |
| `by` | `by` |
| `product` | `product` |
| `reason` | `reason` |
| `rating` | `rating` |
| `count` | `rating` (i18next's plural count; the storefront passes the rating as `count`) |
| `until` | `until` |

**Rule** (`NotificationContract.PlaceholdersFor(kind)`): a kind may use a placeholder when it carries **every** key
listed, whether that key is `required` or `optional` for the kind. The `kinds` section itself did not change.

What that gives each of the 30 kinds at the merge:

| Kind | Placeholders | Kind | Placeholders |
| :-- | :-- | :-- | :-- |
| `OrderPaid` | order, total | `ProductTakenDown` | product, reason |
| `OrderFailed` | order | `NewReview` | count, product, rating |
| `ParcelShipped` | order, shop (optional key), tracking | `ParcelAutoDelivered` | order |
| `OrderCancelled` | by, order | `ReturnRequested` | order |
| `NewSale` | order | `ReturnAccepted` | order |
| `SaleCancelled` | order | `ReturnRefused` | order, reason |
| `ParcelReceived` | order | `ReturnSentBack` | order, tracking |
| `PayoutRecorded` | amount | `ReturnRefunded` | amount, order |
| `ModeratorGranted` | none | `AccountLocked` | reason, until |
| `ModeratorRevoked` | none | `AccountBanned` | reason |
| `ShopApproved` | shop | `ReviewHidden` | product, reason |
| `ShopRejected` | reason, shop | `SavedBackInStock` | product |
| `ProductApproved` | product | `NewQuestion` | product |
| `ProductRejected` | product, reason | `QuestionAnswered` | product |
| | | `QuestionHidden` | product, reason |
| | | `AnswerHidden` | product, reason |

Who reads it:

- **Activity** refuses words using any other placeholder (`NotificationWordingHandlers.Clean`), and returns each kind's
  list in `GET /api/notifications/wording/all`.
- **The storefront's tests**: `describeNotification` fills exactly these names (`FILLED_PLACEHOLDERS`), and every
  bundled sentence in both languages uses only what its kind may (`utils/notifications/index.test.ts`).

A new placeholder is therefore a line in this file, a filling in `describeNotification`, and a test goes red if either
is missing.

---

## MassTransit tables

Unchanged. Activity already had the inbox and outbox tables (`AddTransactionalOutboxEntities()`) for its consumers;
the audit entries a wording change publishes go through the same `OutboxMessage` table, now written from a request as
well as from a consumer.

---

## What did not change, and why that matters

- **No existing table or column was touched.** The migration only creates a table and an index, so an earlier Activity
  image runs against the new schema unchanged, and redeploying it simply ignores the table (constitution, schema
  evolution). `Down` drops the table.
- **With no rows, every notice reads exactly as before** - the public read returns empty `vi` and `en`, and the
  storefront keeps its bundle.
- **`notifications` rows are unchanged.** A notice still stores a kind and data, never a sentence (specs/042); that is
  why an edit applies to old notices as well as new ones.
