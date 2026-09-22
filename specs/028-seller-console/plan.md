# Implementation Plan: A seller can actually sell

**Branch**: `028-seller-console` | **Spec**: [spec.md](spec.md)

## Technical Context

**Backend**: one additive field on `AuthResponse` (contracts/api.md). Nothing else. Every endpoint
this feature drives shipped in specs/027 and is already covered by Bruno and by 109 Catalog tests.

**Frontend**: React 19 / TS / Tailwind v4 / TanStack Query, the conventions in client/README.md -
a folder per thing with an `index.tsx`, `pages/` composing `components/`, `services/` one class per
entity, `hooks/` the query layer.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Untouched. No new cross-service call; the storefront talks only to the gateway. |
| II - Clean Architecture | The `Roles` change is a response DTO in Identity's Application layer, assembled from what `UserManager` already knows. No layer inverted. |
| III - Atomic writes, idempotent messaging | No new message, no new write path. |
| IV - Identity from the token | **The point of the feature.** Not one new endpoint and not one new parameter carries a seller id; `/api/products/mine` and `/api/sellers/me` both read the caller from the token. The roles on the response are a copy of what the token already says, for drawing, never for deciding. |
| V - Evidence over assumption | D5's claim that a seller-listed product has no stock was checked, not assumed: `RegisterProductCommandHandler` writes `QuantityOnHand = 0`. |

No Complexity Tracking entries. The feature adds a field and some pages.

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
