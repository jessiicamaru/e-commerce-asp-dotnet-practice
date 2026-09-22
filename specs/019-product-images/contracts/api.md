# API Contract: Product Images

All through the gateway. The existing route `/api/products/{**catch-all}` already covers these.

## `PUT /api/products/{id}/image` - Admin

`multipart/form-data`, one part named `file`.

| Outcome | Status | Body |
| :-- | :-- | :-- |
| Stored | 200 | `ProductResponse` with the new `imageUrl` |
| Not JPEG/PNG/WebP by content | 400 | ProblemDetails, `errors.File` |
| Empty, or over 2 MB | 400 | ProblemDetails, `errors.File` |
| Body far over the limit | 400 | `errors[""]`: "Request body too large…" - refused before buffering (a 50 MB body is answered in 0.05 s). Not 413: MVC reports a failed form read as model state, verified |
| No `file` part | 400 | |
| Unknown product | 404 | |
| Changed by someone else meanwhile | 409 | |
| Not signed in / not Admin | 401 / 403 | |

## `DELETE /api/products/{id}/image` - Admin

| Outcome | Status |
| :-- | :-- |
| Removed, or there was none | 204 |
| Unknown product | 404 |
| Not signed in / not Admin | 401 / 403 |

## `GET /api/products/{id}/image[?v={version}]` - anonymous

| Outcome | Status | Headers |
| :-- | :-- | :-- |
| `v` matches the current version | 200 | `Content-Type` as stored, `Cache-Control: public, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff` |
| No `v`, or a stale one | 200 | the current image, `Cache-Control: no-cache`, `nosniff` |
| Product has no image, or does not exist | 404 | |

## `ProductResponse` (listing and lookup)

Adds `imageUrl: string | null`. Nothing else changes.
