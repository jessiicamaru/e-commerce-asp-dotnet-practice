# HTTP Contract: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md)

**Base**: Catalog on `http://localhost:5057`, reached through the gateway at
`http://localhost:5000/api/products/{**catch-all}`. No gateway route was added; the existing products
route already covers the path.

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler`.

---

## `DELETE /api/products/{id}` - `Admin` only

Removes a product and every variant of it for good (FR-001). **Not** the way to stop selling
something - that is deactivation, which leaves the row.

`{id}` is constrained to a `guid` in the route (`[HttpDelete("{id:guid}")]`), so a malformed id does not
reach the handler.

**Request**: no body.

**Responses**:

| Status | When | Body |
| :--- | :--- | :--- |
| `204 No Content` | The product, its variants and everything cascading from them were deleted and `ProductDeletedEvent` was staged in the same transaction | none |
| `401 Unauthorized` | No or invalid bearer token | ProblemDetails |
| `403 Forbidden` | The caller is signed in but is not `Admin` (a customer, for example) | ProblemDetails |
| `404 Not Found` | No product has this id | `{"title": "...", "status": 404, "detail": "Product with ID '<id>' was not found."}` (detail hidden outside Development) |

A second `DELETE` of the same id is therefore a `404`, not a `204`: deletion is not idempotent over
HTTP, deliberately, so a script cannot report cleaning up something it never found.

**After a delete**:

- `GET /api/products/{id}` → `404` (an inactive product answers `200` with `isActive: false`; a
  deleted one does not answer at all).
- `GET /api/stock/{variantId}` on Inventory → `404` for every variant, once the event has been consumed.
- Existing orders read exactly as before.

## Authorization

| Endpoint | Access |
| :--- | :--- |
| `DELETE /api/products/{id}` | `[Authorize(Roles = "Admin")]` |

The role comes from the validated token (`RoleClaimType = "role"`); the command carries only the
product id.

Since then: specs/027 widened this to `Seller,Admin`, a seller deleting only their own product (another
seller's is `404`).
