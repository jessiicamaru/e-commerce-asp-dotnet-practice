# HTTP Contract: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Feature**: [spec.md](../spec.md)

One new endpoint. The redesign itself changed no interface: it reads the endpoints the storefront already
used, listed at the end.

**Base**: Catalog on `http://localhost:5057`, through the gateway at `http://localhost:5000`. No gateway
route was added; `/api/categories` already routes to Catalog.

---

## `DELETE /api/categories/{id}` - `Admin` only

Removes a category nothing is filed under (FR-008). `{id}` is route-constrained to a `guid`.

**Request**: no body.

**Responses**:

| Status | When | Body |
| :--- | :--- | :--- |
| `204 No Content` | The category was empty and is deleted | none |
| `401 Unauthorized` | No or invalid bearer token | ProblemDetails |
| `403 Forbidden` | Signed in but not `Admin` | ProblemDetails |
| `404 Not Found` | No category has this id | `detail`: `Category with ID '<id>' was not found.` |
| `409 Conflict` | Products are still filed under it | `detail`: `'<name>' still has <n> product(s) filed under it. Move or delete them first.` |

`detail` is shown in Development only, as for every ProblemDetails from `GlobalExceptionHandler`.

No event is published: no other service holds category rows.

---

## Endpoints the redesigned storefront relies on, unchanged

| Endpoint | Used by |
| :--- | :--- |
| `GET /api/products?pageNumber=&pageSize=&searchTerm=&categoryId=&sortBy=` | Catalogue grid, hero count and featured product |
| `GET /api/categories` | Hero chips and the filter |
| `GET /api/products/{id}` | Product page and variant chooser |
| `GET /api/products/{id}/image?v=` | Product pictures (specs/019) |
| `GET /api/cart` | Cart line count in the top bar, signed in only |

Language and currency are still sent on every request (`Accept-Language`, `X-Currency`).
