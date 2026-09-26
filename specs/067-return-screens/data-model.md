# Data Model: Returning a delivered parcel (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md)

**No table, column, index or migration changed**: this part is client only. The tables are specs/066's
([data-model](../066-parcel-returns/data-model.md)). What the client added is types that mirror the server's
responses.

## Client types - `client/src/services/order/types.ts`

```ts
export type ReturnStatus = 'Requested' | 'Accepted' | 'Refused' | 'Escalated' | 'Rejected' | 'SentBack' | 'Received'

export interface ParcelReturn {
  id: string
  orderId: string
  shipmentId: string
  isShop: boolean              // the shop's own parcel - staff answer it
  status: ReturnStatus
  reason: string
  decisionReason: string | null
  trackingReference: string | null
  requestedAt: string
  decidedAt: string | null     // the buyer's windows after a decision count from here
  sentBackAt: string | null
  receivedAt: string | null
  refundAmount: number | null  // goods plus tax, in the order's currency, once received
}

export interface ReturnPage { items: ParcelReturn[]; page: number; pageSize: number; totalCount: number }
```

`Shipment.return?: ParcelReturn | null` and `Sale.return?: ParcelReturn | null` are optional, so a response from before
specs/066 still type-checks.

## Client constants

| Name | Where | Value |
| :-- | :-- | :-- |
| `RETURN_WINDOW_DAYS` | `constants/order` | `7` - for drawing only (research D1) |
| `RETURN_QUEUE_STATES` | `services/admin/types.ts` | `['Escalated', 'Requested', 'SentBack', 'Received']` - the queue's tabs; accepted, refused and rejected wait on the buyer or are over, and are read on the order |
| `queryKeys.adminReturns(status, page)` | `constants/query-keys` | `['admin-returns', status, page]` |

## What each role is offered, by state (`utils/order/returns.ts`)

| State | Buyer | Seller (`sellerReturnStep`) | Staff (`staffReturnStep`) |
| :-- | :-- | :-- | :-- |
| none, delivered < 7 days ago | request | - | - |
| `Requested` | - | decide | decide, shop's parcel only |
| `Accepted` | send back, within 7 days of `decidedAt` | - | - |
| `Refused` | escalate, within 7 days of `decidedAt` | - | - |
| `Escalated` | - | - | decide (final), any parcel |
| `Rejected` | - | - | - |
| `SentBack` | - | receive | receive, shop's parcel only |
| `Received` | (reads the refund) | - | - |
