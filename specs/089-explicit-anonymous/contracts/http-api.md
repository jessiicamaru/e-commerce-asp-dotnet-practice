# HTTP and gRPC Contract: Signed in unless an endpoint says otherwise

**Feature**: [spec.md](../spec.md)

**No route, request, response or message changes, and no status an existing caller sees changes** (FR-005). What
changes is that each endpoint's access is now declared. This file records that declaration.

## Identity - `AuthController` (`/api/auth`)

| Endpoint | Access | Declared by |
| :-- | :-- | :-- |
| `POST /register` | anonymous | `[AllowAnonymous]` |
| `POST /register-seller` | anonymous | `[AllowAnonymous]` |
| `POST /login` | anonymous | `[AllowAnonymous]` |
| `POST /forgot-password` | anonymous | `[AllowAnonymous]` |
| `POST /reset-password` | anonymous | `[AllowAnonymous]` |
| `POST /confirm-email` | anonymous | `[AllowAnonymous]` |
| `POST /refresh` | anonymous (the HttpOnly cookie is the credential) | `[AllowAnonymous]` |
| `POST /logout` | anonymous (an expired token must not stop a sign-out) | `[AllowAnonymous]` |
| `POST /resend-confirmation` | signed in | class `[Authorize]` (was on the action) |
| `GET /me`, `PUT /me`, `PUT /me/password` | signed in | class `[Authorize]` (was on each action) |

An action added to this controller without an attribute is signed-in, and Identity's `EndpointAccessTests` fails
until it says which.

## Every service that calls `AddJwtAuthentication`

| Endpoint | Access | Declared by |
| :-- | :-- | :-- |
| `GET /health` (Identity, Catalog, Cart, Order, Inventory, Payment, Activity) | anonymous | `.AllowAnonymous()` |
| OpenAPI document (Development only) | anonymous | `.AllowAnonymous()` |
| gRPC `grpc.health.v1.Health`, server reflection (Identity, Catalog, Cart) | anonymous | `.AllowAnonymous()` |
| any other endpoint that declares nothing | **signed in** | fallback policy |

## Catalog gRPC (h2c port; not routed by the gateway)

| Service | Access | Declared by |
| :-- | :-- | :-- |
| `CatalogPricing` (called by Order at quote and checkout, no token) | anonymous | `[AllowAnonymous]` |
| `CatalogOwnership` (called by Inventory before a seller stocks, no token) | anonymous | `[AllowAnonymous]` |

`CartReading` (Cart) and `AddressReading` (Identity) keep `[Authorize]` and receive the customer's forwarded token.

## Not affected

The API gateway and the Orchestrator do not call `AddJwtAuthentication`; their endpoints (`/health`, the
orchestrator's `/`) are unchanged.

## Bruno

No request is added or changed: the collection's anonymous requests (registrations, sign-ins, catalogue reads,
`security-checks/` 401s) are the regression test for FR-005, run against containers rebuilt from this branch.
