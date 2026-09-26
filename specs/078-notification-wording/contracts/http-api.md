# HTTP Contract: An administrator rewords the notifications

> Written on 2026-09-27, after the feature merged (#162), from the code at that merge, the pull request and docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md) | **Data**: [data-model.md](../data-model.md)

Six endpoints on **Activity** (`NotificationWordingController`, `[Route("api/notifications/wording")]`), reached through
the gateway on `:5000` by the routes that already carried `/api/notifications` (`notifications-route`,
`/api/notifications/{**catch-all}`). **No gateway change**: the feature added no route, cluster or rate-limit policy.

Errors are RFC 7807 ProblemDetails from `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
A `ValidationException` is 400 with an `errors` extension keyed by property (`errors.Text`); `NotFoundException` is
404; `ConflictException` is 409.

`{key}` is a declared kind (`NewSale`) or a kind with an i18next plural suffix (`NewReview_one`); `{language}` is `vi`
or `en`. Anything else is **404** `No such notification wording.` on every endpoint that names a key.

---

## `GET /api/notifications/wording` - anonymous

The words the storefront lays over its bundle, for everybody, signed in or not.

`[AllowAnonymous]`, `[ResponseCache(Duration = 60, Location = Any)]` - the response carries
`Cache-Control: public, max-age=60`. Activity keeps no server-side cache.

**200**:

```json
{
  "vi": {},
  "en": {
    "NewSale": "A <strong>new sale</strong>: {{order}}"
  }
}
```

- Both languages are always present, possibly empty.
- Only keys whose **current** version is an edit. A key never edited, or reset, is absent - the storefront shows its
  bundled words for it.
- The words are stored sanitised, with `{{placeholders}}` unfilled. The storefront fills them (escaping each value)
  and sanitises again before showing them.

---

## `GET /api/notifications/wording/all` - `Admin`

What the console needs.

**200**:

```json
{
  "kinds": [
    { "kind": "NewReview", "placeholders": ["count", "product", "rating"] },
    { "kind": "OrderPaid", "placeholders": ["order", "total"] }
  ],
  "entries": [
    {
      "key": "NewSale",
      "language": "en",
      "text": "A <strong>new sale</strong>: {{order}}",
      "isDefault": false,
      "version": 1,
      "updatedAt": "2026-09-26T11:20:00Z",
      "updatedBy": "01999999-0000-7000-8000-000000000001"
    }
  ]
}
```

- `kinds`: every kind in `notification-kinds.json`, ordered by name, each with `NotificationContract.PlaceholdersFor`
  (a placeholder whose data keys the kind carries, required or optional), ordered.
- `entries`: the current version of every key and language **that has one** - edits and resets alike (a reset reads
  `isDefault: true`, `text: null`). Ordered by key, then language. Plural forms appear as their own keys.

---

## `GET /api/notifications/wording/{key}/{language}/versions` - `Admin`

One key's saved versions in one language, **newest first**, each shaped like an `entries` item above.

**200** `[ ... ]` - an empty list for a known key never saved. **404** for an unknown key or language.

---

## `PUT /api/notifications/wording/{key}/{language}` - `Admin`

Reword a notice.

```json
{ "text": "Order <strong>{{order}}</strong> is paid: {{total}}. <a href=\"/orders\">Your orders</a>", "expectedVersion": 0 }
```

`expectedVersion` is the version the editor opened - `0` for a key with no version yet.

What happens, in order:

1. The request validator: `text` not blank (`The words are required.`), at most 500 characters; `expectedVersion >= 0`.
2. The key and language must exist (404).
3. The words are sanitised (`NoticeSanitizer`): `strong`, `b`, `em`, `i`, `u` and `a` with `href` only; `http`/`https`
   schemes; other tags give up their words (a paragraph) or go with them (`script`, `style`, `iframe`, `object`,
   `template`, `noscript`); the result is trimmed.
4. The sanitised words are checked, and every failure is reported at once on `Text`:
   - each `{{placeholder}}` must be one the kind can fill -
     `{{total}} is not something a NewSale notice can fill in. It can use: {{order}}.`;
   - each link must go to `http://`, `https://` or a single-slash page of the shop -
     `A link must go to a web address or a page of the shop, not "//evil.test/x".`;
   - not empty after sanitising (`The words are empty.`), and at most 500 characters.
5. `expectedVersion` must equal the current version, and version `expectedVersion + 1` must still be free.

**200** the new version, shaped like an `entries` item (`isDefault: false`, the sanitised `text`).

| Status | When |
| :-- | :-- |
| 400 | The validator or step 4; `errors.Text` lists every failure |
| 401 | No token |
| 403 | Signed in without `Admin` - a moderator included |
| 404 | Unknown key or language |
| 409 | `Somebody changed these words since you opened them. Reload and make your change again.` - a stale `expectedVersion`, or another save took the number first |

---

## `POST /api/notifications/wording/{key}/{language}/reset` - `Admin`

Back to the storefront's own words, as a new version.

```json
{ "expectedVersion": 1 }
```

**200** the new version: `isDefault: true`, `text: null`.

| Status | When |
| :-- | :-- |
| 401 / 403 | As above |
| 404 | Unknown key or language |
| 409 | `These words are already the storefront's own.` - no version yet, or the current one is a default; or stale, as above |

---

## `POST /api/notifications/wording/{key}/{language}/versions/{version}/restore` - `Admin`

An earlier version's words, as a new version. `{version}` is an integer (route constraint `{version:int}`).

```json
{ "expectedVersion": 2 }
```

The earlier words are **sanitised and checked again** with today's rules (step 3 and 4 above); restoring a default
version adds another default version.

**200** the new version.

| Status | When |
| :-- | :-- |
| 400 | The earlier words fail today's checks |
| 401 / 403 | As above |
| 404 | Unknown key or language; `Version {n} of these words was not found.` |
| 409 | Stale, as above |

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `GET /api/notifications/wording` | Anonymous (`[AllowAnonymous]`) - the storefront needs it before anybody signs in |
| Everything else above | `[Authorize(Roles = "Admin")]` - not `StaffRoles.Staff`; a moderator gets 403 |

The administrator is read from the token through `ICurrentUser` and stored as `CreatedBy`; no request body carries a
user id (Principle IV).

---

## Messages

**No message contract changed.** The feature relies on one that already existed:

| Message | Change |
| :-- | :-- |
| `AuditEntryRecorded` (`Ecommerce.Contracts.Activity`) | Activity now **publishes** it too, through its own outbox (`AddAuditTrail("activity")`), for `NotificationWordingSaved`, `NotificationWordingReset` and `NotificationWordingRestored` (category `System`, subject type `NotificationWording`, subject id `{key}/{language}`, before and after). Activity's existing `RecordAuditEntryConsumer` keeps it, idempotent on the entry id as for every other service. The record's fields are unchanged. |

`UserNotificationRequested` is untouched: a notice still carries a kind and data, and its words are chosen when it is
read.

---

## Storefront calls

`client/src/services/notification-wording` (`NotificationWording.current / overview / versions / save / reset /
restore`), one method per endpoint above, through the shared axios instance at `/api`.
