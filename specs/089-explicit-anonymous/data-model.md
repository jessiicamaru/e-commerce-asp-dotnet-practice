# Data Model: Signed in unless an endpoint says otherwise

**No table, column, index or migration changes** (FR-006). This feature changes who may reach an endpoint, not
what any service stores.

## What decides access, before and after

| Endpoint has | Before | After |
| :-- | :-- | :-- |
| `[Authorize]` / `[Authorize(Roles = ...)]` (itself or its controller) | signed in (and the role) | unchanged |
| `[AllowAnonymous]` / `.AllowAnonymous()` | anyone | unchanged |
| **nothing** | **anyone** | **signed in** (fallback policy) |

`[AllowAnonymous]` wins over a class-level `[Authorize]`, as ASP.NET Core defines it - which is how
`AuthController` is signed-in by default with eight public actions.

## State

There is no persisted state. The decision per request:

```text
request ──▶ endpoint metadata
              ├── IAllowAnonymous ─────────────────────────────▶ served
              ├── IAuthorizeData (roles/policy) ──▶ evaluate ──▶ served / 401 / 403
              └── none ──▶ fallback: authenticated user? ──────▶ served / 401
```

A token revoked under specs/065 fails authentication, so it is refused on both of the lower paths.
