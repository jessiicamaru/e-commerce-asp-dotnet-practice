# Tasks: A seller can actually sell

## Phase 1: The server says who you are

- [ ] T001 Add `Roles` to `AuthResponse` in server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Common/AuthResponse.cs
- [ ] T002 Fill it on login, register, register-seller and refresh in server/src/Services/Identity/Ecommerce.Identity.Application/Auth/
- [ ] T003 Identity tests: a seller's token response carries Seller and Customer, a customer's carries Customer only, in server/tests/Ecommerce.Identity.Tests/

## Phase 2: The storefront knows

- [ ] T004 Add `roles` to `User` and `AuthResponse` in client/src/services/auth/types.ts
- [ ] T005 Carry roles through `accept()` and expose `isSeller` in client/src/context/auth/
- [ ] T006 [P] `RequireRole` beside `RequireAuth` in client/src/components/auth/require-role/index.tsx
- [ ] T007 [P] A shop link for sellers only in client/src/components/layout/top-bar/index.tsx

## Phase 3: US1 - a seller sees their listings

- [ ] T008 `Product.mine()` in client/src/services/product/index.ts
- [ ] T009 `useMyProducts` in client/src/hooks/product/
- [ ] T010 The shop page at client/src/pages/shop/index.tsx, with an empty state
- [ ] T011 Routes `/shop` behind `RequireRole` in client/src/routes/index.tsx

## Phase 4: US2 - listing a product

- [ ] T012 `ServerError` - shows a ProblemDetails detail in words - in client/src/components/shared/server-error/index.tsx
- [ ] T013 `Product.create()` in client/src/services/product/index.ts
- [ ] T014 The create form at client/src/pages/shop-product-new/index.tsx, stating the one-language/one-currency/no-stock consequences from research D2 and D5
- [ ] T015 Route `/shop/products/new`

## Phase 5: US3 - correcting and withdrawing

- [ ] T016 `Product.setPrice()`, `Product.uploadImage()`, `Product.remove()` in the service
- [ ] T017 The seller's product page at client/src/pages/shop-product/index.tsx
- [ ] T018 Route `/shop/products/:id`

## Phase 6: US4 - the shop name

- [ ] T019 `Seller` service and `useMyShop` hook
- [ ] T020 Rename on the shop page

## Phase 7: Polish

- [ ] T021 Every new string in client/src/locales/vi/*.json and en/*.json
- [ ] T022 Bruno: assert `roles` on the login response, in bruno/
- [ ] T023 CLAUDE.md and client/README.md
- [ ] T024 Verify SC-002 against the running stack with two sellers' real tokens
- [ ] T025 Headless screenshots of each new page, both languages
