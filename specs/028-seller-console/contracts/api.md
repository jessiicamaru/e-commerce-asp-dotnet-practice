# Contracts: A seller can actually sell

## The one backend change

### `AuthResponse` gains `Roles`

```diff
 public record AuthResponse(
     Guid Id,
     string Email,
     string FirstName,
     string LastName,
     string Token,
-    string RefreshToken
+    string RefreshToken,
+    IReadOnlyList<string> Roles
 );
```

Returned by **all four** paths that issue a token, because a reload must restore the same answer a
sign-in gave:

| Endpoint | Roles for a plain customer | for a seller |
| :-- | :-- | :-- |
| `POST /api/auth/register` | `["Customer"]` | - |
| `POST /api/auth/register-seller` | - | `["Seller","Customer"]` |
| `POST /api/auth/login` | `["Customer"]` | `["Seller","Customer"]` |
| `POST /api/auth/refresh` | `["Customer"]` | `["Seller","Customer"]` |

**Additive.** An older client ignores the field. Order is not guaranteed and nothing may depend on
it; the storefront asks `includes("Seller")`.

**It is not a permission.** It says what the server already put in the token, so the browser can
decide what to draw. Authorization stays where it is: the controller attributes and
`SellerOwnership`.

## Endpoints the storefront now drives (all already exist, specs/027)

| Method | Path | Who | Used by |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/products/mine` | Seller | the shop page |
| `POST` | `/api/products` | Seller, Admin | the create form |
| `PUT` | `/api/products/{id}/variants/{variantId}/prices/{currency}` | Seller, Admin | price edit |
| `PUT` | `/api/products/{id}/image` | Seller, Admin | image upload |
| `DELETE` | `/api/products/{id}` | Seller, Admin | withdraw |
| `GET` | `/api/sellers/me` | Seller | shop name |
| `PUT` | `/api/sellers/me/shop-name` | Seller | rename |

**None of these gains a seller id parameter.** Whose products, whose shop: from the token.

## What a refusal looks like on the wire

Unchanged, and now shown to a person. RFC 7807, and since specs/022 the `detail` survives outside
Development for the three mapped domain exceptions:

```json
{ "status": 409, "title": "Conflict", "detail": "A product with SKU 'X100' already exists." }
{ "status": 400, "title": "Validation error", "detail": "0.99 is not an amount VND can hold.",
  "errors": { "Price": ["..."] } }
{ "status": 404, "title": "Not found", "detail": "Product with ID '...' was not found." }
```

The 404 is what a seller gets for somebody else's product. The storefront must **not** translate it
into "you are not allowed" - that would undo the reason it is a 404.
