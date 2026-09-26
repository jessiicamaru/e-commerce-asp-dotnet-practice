# Implementation Plan: An administrator's console

> Completed on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md.

**Branch**: `038-admin-console` | **Spec**: [spec.md](spec.md) | [contracts](contracts/api.md)

## Summary

Give administrators a console in the storefront for the two jobs that were Bruno-only: working the shop's
own parcels and settling sellers. The server gains one endpoint, `GET /api/orders/fulfilment/{id}`
(Admin) - the one read of an order not scoped to its owner - because staff could not see what they were
shipping. Everything else calls endpoints that already existed. The client gains `/admin` (queue per
state), `/admin/orders/:id` and `/admin/payouts`, drawn only for an administrator; the seller's parcel
buttons become one shared component. The follow-up #83 made a confirmed dialog close, a defect #82's
tests found. Decisions in [research.md](research.md).

## Technical Context

- **Order**: `GetOrderForStaffQuery` → `OrderMapping.ToDetail` over `IOrderRepository.GetByIdAsync`
  (items and parts loaded); route `GET /api/orders/fulfilment/{id}`, `[Authorize(Roles = "Admin")]`.
  No other server change - every other action already has an endpoint.
- **Client**:
  - `isAdmin` on the auth context (from `roles`, like `isSeller` - for drawing, never deciding).
  - `layouts/admin-layout`, routes `/admin` (fulfilment queue), `/admin/orders/:id`, `/admin/payouts`,
    all under `RequireRole role="Admin"`; a header link and a user-menu entry for administrators.
  - `SaleActions` becomes `components/order/parcel-actions`, taking a status, a tracking reference and
    the two mutations - one component for a seller's parcel and the shop's, because the steps and the
    rule (next step only) are the same.
  - `services/admin` (`Admin.fulfilment`, `Admin.order`, `Admin.prepare`, `Admin.ship`, `Admin.due`,
    `Admin.pay`), `hooks/admin`, namespace `admin` in `locales/`.

**Language/Version**: C# / .NET 10; TypeScript / React 19, Vite, Tailwind v4, shadcn/ui (base-ui),
TanStack Query, react-i18next

**Storage**: none new; reads `orders`, `order_items`, `order_shipments` in `ecommerce_order_db`

**Testing**: xUnit against real PostgreSQL (`FulfilmentTests`, +2); Vitest with Testing Library (+22 in
#82, +2 in #83); Bruno (a staff read and two 403s); `verify-saga.sh`; the console driven in a browser

**Target Platform**: Order on 5059 via the gateway on 5000; the storefront through Vite's `/api` proxy

**Constraints**: the role on the route is the whole permission - the handler must never sit behind any
other route; the client's `isAdmin` draws and never decides; the console fits a 390 px phone

**Scale/Scope**: one administrator at a time; queues paged at `PAGE_SIZE`

## Research

- **D1 - The shop's parcel is read from `shipments[]`**, the part with `isShop`. An order an older image
  wrote has no parts; then the order's own status is the shop's (specs/035 D3).
- **D2 - Staff see the whole order**, including the customer's address and every seller's parcel:
  staff are the shop, and the fulfilment queue already shows them every order's total.
- **D3 - Pay is confirmed in an AlertDialog** naming the shop and the amount the list showed. The
  server pays what is due *now*, which can be more if another parcel shipped meanwhile; the toast shows
  what was actually recorded.

These three are restated with their rationale and alternatives in [research.md](research.md), with
three more (D4-D6) the code and the pull requests show.

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** One read in Order, on Order's data. |
| II - Clean Architecture | **Pass.** Query in Application, route in WebApi. |
| III - Atomic writes | **Pass.** No new write. |
| IV - Identity from the token | **Pass.** Admin role from the token; the client's `isAdmin` only draws. |
| V - Evidence | **Pass.** Test: staff read any order; 403 for customer and seller in Bruno; client tests for queue, actions, payout confirm. |
| Schema compatibility | **Pass.** No migration. |

**Post-design re-check**: unchanged. The staff read has no owner in its query by design (D4); Principle
IV is met because the permission is the role in the validated token, and Bruno proves the refusal with
real customer and seller tokens.

## Project Structure

### Documentation (this feature)

```text
specs/038-admin-console/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D6
├── data-model.md            # no schema change
├── quickstart.md
├── contracts/
│   └── api.md               # the one new endpoint and those it uses
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull requests)

```text
server/src/Services/Order/
├── Ecommerce.Order.Application/Orders/Queries/GetOrderForStaff/GetOrderForStaffQuery.cs   # new
└── Ecommerce.Order.WebApi/Controllers/OrdersController.cs                                 # GET fulfilment/{id}
server/tests/Ecommerce.Order.Tests/FulfilmentTests.cs                                      # +2
client/src/
├── context/auth/{index.tsx,types.ts}                         # isAdmin
├── services/admin/{index.ts,types.ts,index.test.ts}          # new
├── hooks/admin/index.ts                                      # new
├── constants/query-keys/index.ts, config/i18n/index.ts
├── layouts/admin-layout/index.tsx                            # new
├── pages/admin-orders/, admin-order/ (with shop-parcel.ts), admin-payouts/   # new, with tests
├── components/order/parcel-actions/index.tsx                 # was SaleActions
├── components/layout/{top-bar,user-menu}/                    # the entry, administrators only
├── routes/index.tsx                                          # /admin under RequireRole role="Admin"
├── locales/{en,vi}/admin.json                                # new
├── test/{refusal.ts,render.tsx}
└── components/ui/alert-dialog.tsx                            # #83: AlertDialogAction is a Close
client/src/components/address/address-card/index.test.tsx     # #83: closes the dialog once confirmed
client/src/layouts/seller-layout/index.tsx                    # #83: four tabs fit a phone
client/README.md                                              # #83: the fourth edited ui/ file
bruno/admin-audit/staff read any order.yml
bruno/security-checks/a customer cannot read an order as staff is 403.yml
bruno/seller/a seller cannot read an order as staff is 403.yml
CLAUDE.md, .specify/feature.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## Verification

Order tests; client lint/test/build; Bruno; `verify-saga.sh`; the console driven end to end in a browser.

**What the pull requests recorded**: #82 - Order **140** tests (+2, both seen red against a stub handler
first), all server tests passing; client **143** (+22), lint clean, build passing; seven mutations each
turning a client test red; Bruno **114/114** requests, **182/182** tests; `verify-saga.sh` passing; in a
browser as the administrator, a mixed order (the shop's Canon, Tuấn's battery) prepared and shipped from
the console with a tracking reference while Tuấn's parcel stayed waiting, Tuấn paid out with the toast
"Đã ghi nhận 1.716.000 ₫", and no horizontal overflow at 390 px. #83 - client **145**, lint clean, build
passing, the suite run twice; the new `address-card` test red before the fix; screenshots of the seller
console at 390 px and 1360 px with all four tabs on screen.

## What this feature does not finish

- Product and category moderation, payment and reservation audit, and user management - later
  features (specs/041, 043, 045).
- A per-order payout, or paying less than is due.
- Cancelling an order from the console - specs/039 added it.
