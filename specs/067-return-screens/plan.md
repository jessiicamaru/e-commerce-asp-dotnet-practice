# Implementation Plan: Returning a delivered parcel (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Branch**: `067-return-screens` | **Spec**: [spec.md](spec.md) | **Issue**: #107 | **PR**: #151 (merged 2026-09-25)

## Summary

The storefront screens for the return flow built on the server in specs/066: the buyer's steps under each parcel of
`/orders/:id`, the seller's on `/shop/sales/:id`, staff's on `/admin/orders/:id`, and a new `/admin/returns` queue.
Pages decide only what to offer, by copying each server guard into pure functions; the server still refuses on its
own.

## Technical Context

**Language/Version**: TypeScript, React 19

**Primary Dependencies**: Vite, Tailwind v4, shadcn/ui, axios, TanStack Query, react-i18next

**Storage**: none - the server holds every return

**Testing**: Vitest with jsdom and Testing Library (`npm test` in `client/`); oxlint; `tsc -b`

**Target Platform**: the storefront, through the gateway (Vite proxy in development, nginx `/api` in the container)

**Project Type**: web front end

**Constraints**: no server change; every sentence in Vietnamese and English; one page size, `PAGE_SIZE` = 12

**Scale/Scope**: four pages touched or added, three new components, 10 new service calls

## Design

Client only. No endpoint, message or table changes.

- **Types** (`services/order/types.ts`):
  - `ParcelReturn` mirrors `ReturnResponse`;
  - `Shipment.return` and `Sale.return` are optional and nullable;
  - `ReturnPage`.
- **Service calls:**
  - `Order.requestReturn`, `escalateReturn` and `sendReturnBack` for the buyer;
  - `Order.acceptSaleReturn`, `refuseSaleReturn` and `receiveSaleReturn` for the seller;
  - `Admin.returns`, `acceptReturn`, `refuseReturn` and `receiveReturn` for staff.
- **Rules**: `utils/order/returns.ts` holds the pure functions that decide what to *offer*:
  - `canRequestReturn`, `canEscalate`, `canSendBack` and `returnDeadline` for the buyer;
  - `sellerReturnStep` and `staffReturnStep` for the other two, each returning `decide`, `receive` or
    nothing.

  Each copies the matching server guard. `RETURN_WINDOW_DAYS` lives in `constants/order`.
- **Components**:
  - `components/order/parcel-return` is the buyer's view of one parcel. It uses its own hook,
    `useParcelReturn(orderId, shipmentId)`, so every parcel's mutations are its own.
  - `components/order/return-decision` is the seller's and staff's view: the reason, the state, and the
    accept, refuse and received steps, with the mutations handed in (the same idea as `ParcelActions`). The
    `final` flag words a refusal as "reject for good".
- **Pages**:
  - `pages/order` draws `ParcelReturn` under the single parcel. `OrderShipments` draws it inside each card
    when given `orderId`.
  - `pages/shop-sale` adds a return card when `sale.return` exists.
  - `pages/admin-order` adds a returns card with one `StaffReturn` per parcel that has a return.
  - The new `pages/admin-returns` is the queue, with its status in `?status=`. It is routed at
    `/admin/returns` and added to the menu as `adminOnly`.
- **Shared dialog** (found while building, per the PR): `components/shared/text-prompt` - used for the request, the
  refusal and the tracking reference. It will not send empty text, closes only once the server accepts, and shows a
  refusal inside the dialog.

## Research

- **D1 - how the client learns the 7 days.** It uses a constant, `RETURN_WINDOW_DAYS = 7`, for *drawing
  only*.
  - The alternative was a `returnableUntil` field on `ShipmentResponse`. That would have meant a server change
    to a static mapping that has no options, in a PR that is otherwise client-only.
  - If the two ever disagree, the server refuses with a 409 that names the window, and the page shows it. The
    same bargain as `customerCanCancel`.
- **D2 - one decision component for the seller and staff.** Same steps, same confirmation. Only the
  endpoints differ, and those are handed in. The same idea as `ParcelActions` (specs/038).
- **D3 - the queue shows no amounts.** `ReturnResponse` carries no currency, and a number without its
  currency is the thing specs/022 forbids. The amount is on the order page, in the order's currency.

The same decisions, with their alternatives set out in full, are in [research.md](research.md).

## Constitution check

- **IV (identity from the token):** no id of the caller is sent anywhere. The server decides whose parcel it
  is.
- **V (evidence):** there are Vitest tests for every rule and action, and the whole round trip through the
  storefront container is recorded in the PR.

Against all five principles of [constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client decides nothing that counts: it copies the server's guards only to choose which button to draw, and the server stays the one owner of the return's state and window (research D1) |
| **II. Clean Architecture Layering** | **Pass (not applicable to the server).** No server code changed. The client follows its own layering from `client/README.md`: `services/` over axios, `hooks/` as the TanStack Query layer, `pages/` composing `components/`, rules in `utils/order` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not applicable).** No write path on the server changed. A step pressed twice reaches the server's guarded statement and answers 409, which the page shows |
| **IV. Identity Comes From the Token** | **Pass.** No request carries the caller's id; seller routes name only the order, and the server reads the rest from the token |
| **V. Evidence Over Assumption** | **Pass, with one gap stated.** Vitest 371/371, 9 of 9 mutations caught, Bruno 215/215 through the rebuilt storefront container's nginx, the served bundle checked for the new routes and words, `/admin/returns` deep link 200. **Not done: clicking through in a real browser** - said so in the PR |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/067-return-screens/
├── spec.md
├── plan.md              # this file
├── research.md          # D1-D4
├── data-model.md        # the client types; no table changed
├── contracts/README.md  # the endpoints relied on; none changed
├── quickstart.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (touched at the merge)

```text
client/src/
├── services/order/{index.ts, types.ts, index.test.ts}
├── services/admin/{index.ts, types.ts, index.test.ts}
├── hooks/{order,admin}/index.ts
├── constants/{order,query-keys}/index.ts
├── utils/order/{returns.ts, returns.test.ts}
├── components/order/{parcel-return, return-decision, order-shipments}/index.tsx
├── components/shared/text-prompt/index.tsx
├── pages/order/{index.tsx, index.test.tsx}
├── pages/shop-sale/{index.tsx, index.test.tsx}
├── pages/admin-order/{index.tsx, staff-return.tsx, index.test.tsx}
├── pages/admin-returns/{index.tsx, index.test.tsx}
├── layouts/admin-layout/{index.tsx, index.test.tsx}
├── routes/index.tsx
└── locales/{en,vi}/{orders,seller,admin}.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- No real-browser click-through was done at merge (Playwright came later, specs/080).
- No return badge on the sales list.
- The window lives in two places (server option, client constant); a change to one without the other draws buttons the
  server refuses - safely, with its 409.
