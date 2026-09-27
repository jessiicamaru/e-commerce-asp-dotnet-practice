# HTTP Contract: Administrators manage categories

**Feature**: [spec.md](../spec.md)

## New - `PUT /api/categories/{id}` (Admin)

```json
{ "name": "Ống kính rời", "description": "Cho Sony E" }
```

| Status | When |
| :-- | :-- |
| 200 | `CategoryResponse` - the slug unchanged |
| 400 | empty name, name > 100, description > 500 (`errors`) |
| 401 / 403 | no token / not Admin |
| 404 | no such category |

## Changed - `POST /api/categories` (Admin)

| Status | Before | After |
| :-- | :-- | :-- |
| slug already taken | **500** (bare `Exception`) | **409** "A category with the address '<slug>' already exists." |

## Used by the page (unchanged)

- `GET /api/categories?lang=vi|en` - each category as it reads in that language, with `language` saying which it is in.
- `PUT` / `DELETE /api/categories/{id}/translations/en` - the English (specs/026).
- `DELETE /api/categories/{id}` - 409 while products are filed under it (specs/024).

## Bruno

`category/` seq 3 rename (200, slug kept), seq 6 a customer's rename (403), seq 7 a second category at the same address (409).
