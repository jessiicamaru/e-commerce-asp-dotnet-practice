# HTTP Contract: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md)

**Base**: Catalog on `http://localhost:5057`, through the gateway at `http://localhost:5000`. No gateway
route was added. Every request's language is negotiated as in specs/021: `?lang=`, then `Accept-Language`,
then the default; responses carry `Content-Language` and `Vary: Accept-Language`.

---

## `GET /api/categories` - anonymous (changed)

Each item gains `language`, and `name` / `description` now follow the request's language with per-field
fallback.

```json
[
  {
    "id": "0199...",
    "name": "Mirrorless cameras",
    "description": "Mirrorless bodies with interchangeable lenses",
    "slug": "may-anh-mirrorless",
    "parentCategoryId": null,
    "isActive": true,
    "language": "en"
  }
]
```

`language` is the language the text is actually in: the requested one when a translation exists, the
default (`vi`) when it does not.

---

## `PUT /api/categories/{id}/translations/{language}` - `Admin` only

Creates or replaces the category's text in one language.

```json
{ "name": "Mirrorless cameras", "description": "Mirrorless bodies with interchangeable lenses" }
```

| Status | When | Body |
| :--- | :--- | :--- |
| `200 OK` | Stored (created or replaced) | The category in that language, `language` set to it |
| `400 Bad Request` | `name` empty or over 100; `description` over 500; `language` not one the shop speaks | ProblemDetails with `errors` |
| `401` / `403` | Not signed in / not `Admin` | ProblemDetails |
| `404 Not Found` | No category has this id | `Category with ID '<id>' was not found.` |

`{language}` is lower-cased before it is stored.

---

## `DELETE /api/categories/{id}/translations/{language}` - `Admin` only

Removes one language; the category falls back to its own text.

| Status | When |
| :--- | :--- |
| `204 No Content` | Removed, **or there was none to remove** |
| `400 Bad Request` | `language` not one the shop speaks |
| `401` / `403` | Not signed in / not `Admin` |
| `404 Not Found` | No category has this id |

---

## Messages

None. No other service reads category text.
