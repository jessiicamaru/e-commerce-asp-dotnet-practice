# Implementation Plan: A seller can actually sell

> Completed on 2026-09-27, after the feature merged (#65), from the code at that merge, the pull request, docs/features/marketplace.md and docs/architecture/storefront.md.

**Branch**: `028-seller-console` | **Spec**: [spec.md](spec.md)

## Summary

One backend change - `Roles` on `AuthResponse`, filled from `user.Roles` on login, register,
register-seller and refresh - and three seller pages in the storefront: `/shop` (own listings, shop name,
rename), `/shop/products/new` (one-form create, price in the default currency) and `/shop/products/:id`
(price per currency, image, withdraw), behind `RequireRole`, with `ServerError` showing the server's own
words. And the client's first unit tests: Vitest with jsdom and Testing Library, a setup file that fails
any test reaching the network, 33 tests, and `npm test` in CI.

## Technical Context

**Backend**: one additive field on `AuthResponse` (contracts/api.md). Nothing else. Every endpoint
this feature drives shipped in specs/027 and is already covered by Bruno and by 109 Catalog tests.

**Frontend**: React 19 / TS / Tailwind v4 / TanStack Query, the conventions in client/README.md -
a folder per thing with an `index.tsx`, `pages/` composing `components/`, `services/` one class per
entity, `hooks/` the query layer.

**Testing** (added while building): Vitest 5 with jsdom and Testing Library (`@testing-library/react`,
`user-event`, `jest-dom`), configured in `client/vitest.config.ts` separately from `vite.config.ts` so no
test inherits the dev proxy; `client/src/test/setup.ts` stubs `fetch` to throw. Identity's new
`SellerRolesTests` run against a real PostgreSQL like the rest.

**Storage**: none changed ([data-model.md](./data-model.md)).

**Target Platform**: the storefront through Vite's dev proxy (`/api` → `:5000`); Identity (5056).

**Constraints**: roles are for drawing, never deciding; no new parameter carries a seller id; the create
price is the default currency's; a 404 stays a 404 in the interface.

**Scale/Scope**: one response field, four Identity handlers, three pages, two services, two hooks, one
guard, one shared component, 33 client tests and 4 Identity tests.

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The verdicts were
written without an explicit Pass/Fail; each is a **Pass**, stated on 2026-09-27.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Untouched. No new cross-service call; the storefront talks only to the gateway. |
| II - Clean Architecture | **Pass.** The `Roles` change is a response DTO in Identity's Application layer, assembled from what `UserManager` already knows. No layer inverted. |
| III - Atomic writes, idempotent messaging | **Pass.** No new message, no new write path. |
| IV - Identity from the token | **Pass - the point of the feature.** Not one new endpoint and not one new parameter carries a seller id; `/api/products/mine` and `/api/sellers/me` both read the caller from the token. The roles on the response are a copy of what the token already says, for drawing, never for deciding. |
| V - Evidence over assumption | **Pass.** D5's claim that a seller-listed product has no stock was checked, not assumed: `RegisterProductCommandHandler` writes `QuantityOnHand = 0`. |

**Post-design re-check** (2026-09-27, against the merged code): no violations. `Roles` is read from the
same `user.Roles` collection the token's claims are built from, and a Bruno test asserts the response and
the token agree. Principle V was served beyond the plan: SC-002 was run against the API with two real
sellers' tokens, and the client gained its first tests.

## Complexity Tracking

No Complexity Tracking entries. The feature adds a field and some pages.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## The trap this feature must not fall into

specs/027 was nearly shipped broken because the controller attribute said `Admin` while the
ownership check said seller-or-admin: every unit test passed and a real seller got 403 on her own
product. The symmetric trap here is the opposite one - **the storefront hiding a button is not a
refusal.** If any check in this feature exists only in React, it does not exist. The acceptance for
SC-002 is therefore run against the API with a second seller's token, not against the page.

## Phases

**Phase 1 - the server says who you are.** `Roles` on `AuthResponse`, filled on all four paths.
Identity tests cover that a seller's login carries both roles and a customer's carries one.

**Phase 2 - the storefront knows.** `User.roles`, `useAuth().isSeller`, `RequireRole`. A shop link
in the top bar for a seller and for nobody else.

**Phase 3 - the shop page (US1).** `Product.mine()`, `useMyProducts`, `/shop`.

**Phase 4 - listing a product (US2).** The create form, and the shared component that shows a
server refusal in words.

**Phase 5 - correcting and withdrawing (US3).** Price, image, delete, on the product's own page.

**Phase 6 - the shop name (US4).** `Seller.me()`, rename.

**Phase 7 - translations, Bruno, docs.** Both languages for every string added. A Bruno request for
the `Roles` field. CLAUDE.md and client/README.md.

## Verification

- Identity tests for Phase 1, run with a real database like the rest.
- `npm run lint`, `tsc`, `build` - the CI `client` job.
- **Against the running stack**: register a second seller, list a product as each, and confirm
  through the API that neither can write the other's. That is SC-002 and it is not a UI test.
- A headless screenshot of each new page in both languages, because this project has learned that
  a page nobody looked at is a page that does not work.

## Project Structure

### Documentation (this feature)

```text
specs/028-seller-console/
├── spec.md
├── plan.md               # This file
├── research.md           # D1-D5
├── data-model.md         # Added 2026-09-27: no table changed; the response and client shapes
├── contracts/api.md      # Roles on AuthResponse; the endpoints the pages drive
├── quickstart.md         # Added 2026-09-27
├── checklists/requirements.md   # Added 2026-09-27
└── tasks.md
```

### Source Code (as changed by #65)

```text
server/src/Services/Identity/Ecommerce.Identity.Application/Auth/
├── Common/AuthResponse.cs                                    # + Roles
└── Commands/{Login,Register,RegisterSeller,Refresh}/...      # fill Roles from user.Roles
server/tests/Ecommerce.Identity.Tests/
├── SellerRolesTests.cs                                       # new, 4 tests
└── IdentityTestFixture.cs                                    # roles seeded from RoleNames.Descriptions

client/
├── package.json, package-lock.json, vitest.config.ts, tsconfig.app.json   # Vitest, jsdom, Testing Library
└── src/
    ├── test/setup.ts                                         # fails any test that reaches the network
    ├── components/auth/require-role/index.tsx (+ .test)      # RequireRole
    ├── components/shared/server-error/index.tsx (+ .test)    # ServerError
    ├── components/layout/top-bar/index.tsx                   # shop link for sellers
    ├── config/axios/index.ts                                 # X-Currency ??= (a caller's header wins)
    ├── config/i18n/index.ts, locales/{en,vi}/seller.json     # the seller namespace
    ├── constants/query-keys/index.ts
    ├── context/auth/{index.tsx,types.ts}                     # roles, isSeller
    ├── services/auth/types.ts                                # roles on the response and User
    ├── services/product/{index.ts,types.ts} (+ .test)        # mine, create, get(id, currency), setPrice, removePrice, uploadImage, remove
    ├── services/seller/{index.ts,types.ts}                   # me, rename
    ├── hooks/product/index.ts (+ .test)                      # useMyProducts, useProductInEveryCurrency, mutations
    ├── hooks/seller/index.ts                                 # useMyShop, useRenameShop
    ├── pages/shop/index.tsx (+ .test)
    ├── pages/shop-product-new/index.tsx (+ .test)
    ├── pages/shop-product/index.tsx (+ .test)
    └── routes/index.tsx                                      # three routes behind RequireRole

bruno/auth/login admin.yml, bruno/auth/login customer.yml, bruno/seller/register a seller.yml
.github/workflows/ci.yml                                      # npm test in the client job
CLAUDE.md, client/README.md
```

## What this feature does not finish

- **An administrator console** (specs/038), **variants beyond the first** from the browser, and **a
  seller setting their own stock** (specs/031) - a new listing has no stock and the form says so.
- **The image-over-prices layout defect** was found in a screenshot and fixed; "there is no honest unit
  test for it".
- **Withdrawing a product left its image file behind** at this merge; specs/029 fixed deletion.
- **Roles granted later arrive at the next refresh** - the response reports what the token holds.
