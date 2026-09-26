# Data Model: A seller can actually sell

> Written on 2026-09-27, after the feature merged (#65), from the code at that merge, the pull request, docs/features/marketplace.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** #65 contains no migration in any service.
Everything the seller pages read and write - `products.SellerId`, the `sellers` read model, `seller_profiles`,
the `Seller` role - arrived in specs/027. What changed is the shape of one response and the client's own
models.

---

## `AuthResponse` (Identity, response only)

| Field | Type | Source | Note |
| :--- | :--- | :--- | :--- |
| `id`, `email`, `firstName`, `lastName`, `token`, `refreshToken` | unchanged | | |
| `roles` | `string[]` (`IReadOnlyList<string>`) | `user.Roles.Select(role => role.Name)` | **New.** The same collection the token's `role` claims are built from. Order not guaranteed |

Returned by login, register, register-seller and refresh. Additive: an older client ignores it.

---

## Identity test fixture

`IdentityTestFixture` now seeds the `roles` table from `RoleNames.Descriptions` - the list `DataInitializer`
reads - instead of one hand-written `Customer` row. That is test data, not schema, but it is why specs/027's
`Seller` role had broken three tests the moment they asked for it.

---

## Client models (`client/src/services/...`)

| Type | Field(s) added | Why |
| :--- | :--- | :--- |
| `AuthResponse`, `User` (`services/auth/types.ts`) | `roles: string[]` | `useAuth().isSeller`, `RequireRole` |
| `NewProduct` (`services/product/types.ts`) | `name`, `description` (nullable), `price`, `sku`, `categoryId` - and deliberately no seller field | The one-form create; `price` is the default currency's amount |
| `Shop` (`services/seller/types.ts`) | `sellerId`, `shopName` | `/api/sellers/me`; there is no type for anybody else's shop because no endpoint returns one |

Query keys gained `myProducts(query)` and `myShop()` (`constants/query-keys`), so a write invalidates
exactly what it changed.

Nothing is persisted in the browser: roles live in the in-memory auth context with the access token, and
are restored by the refresh response on reload.
