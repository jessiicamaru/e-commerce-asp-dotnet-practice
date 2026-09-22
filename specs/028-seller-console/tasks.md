# Tasks: A seller can actually sell

## Phase 1: The server says who you are

- [X] T001 Add `Roles` to `AuthResponse` in server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Common/AuthResponse.cs
- [X] T002 Fill it on login, register, register-seller and refresh in server/src/Services/Identity/Ecommerce.Identity.Application/Auth/
- [X] T003 Identity tests: a seller's token response carries Seller and Customer, a customer's carries Customer only, in server/tests/Ecommerce.Identity.Tests/

## Phase 2: The storefront knows

- [X] T004 Add `roles` to `User` and `AuthResponse` in client/src/services/auth/types.ts
- [X] T005 Carry roles through `accept()` and expose `isSeller` in client/src/context/auth/
- [X] T006 [P] `RequireRole` beside `RequireAuth` in client/src/components/auth/require-role/index.tsx
- [X] T007 [P] A shop link for sellers only in client/src/components/layout/top-bar/index.tsx

## Phase 3: US1 - a seller sees their listings

- [X] T008 `Product.mine()` in client/src/services/product/index.ts
- [X] T009 `useMyProducts` in client/src/hooks/product/
- [X] T010 The shop page at client/src/pages/shop/index.tsx, with an empty state
- [X] T011 Routes `/shop` behind `RequireRole` in client/src/routes/index.tsx

## Phase 4: US2 - listing a product

- [X] T012 `ServerError` - shows a ProblemDetails detail in words - in client/src/components/shared/server-error/index.tsx
- [X] T013 `Product.create()` in client/src/services/product/index.ts
- [X] T014 The create form at client/src/pages/shop-product-new/index.tsx, stating the one-language/one-currency/no-stock consequences from research D2 and D5
- [X] T015 Route `/shop/products/new`

## Phase 5: US3 - correcting and withdrawing

- [X] T016 `Product.setPrice()`, `Product.uploadImage()`, `Product.remove()` in the service
- [X] T017 The seller's product page at client/src/pages/shop-product/index.tsx
- [X] T018 Route `/shop/products/:id`

## Phase 6: US4 - the shop name

- [X] T019 `Seller` service and `useMyShop` hook
- [X] T020 Rename on the shop page

## Phase 7: Polish

- [X] T021 Every new string in client/src/locales/vi/*.json and en/*.json
- [X] T022 Bruno: assert `roles` on the login response, in bruno/
- [X] T023 CLAUDE.md and client/README.md
- [X] T024 Verify SC-002 against the running stack with two sellers' real tokens
- [X] T025 Headless screenshots of each new page, both languages

## Added while building

- [X] T026 Vitest, jsdom and Testing Library in client/, and `npm test` in the CI `client` job - the
      storefront had no tests at all, so it could render nothing and still go green
- [X] T027 Seed the Identity test fixture from `RoleNames.Descriptions` rather than one hand-written
      Customer row, which is why specs/027's Seller role broke three tests the moment they asked for it
- [X] T028 `Product.get(id, currency)` and `useProductInEveryCurrency` - a response carries one
      currency's prices, so the price editor reads the product once per currency
- [X] T029 The axios interceptor sets `X-Currency` only when the caller did not
