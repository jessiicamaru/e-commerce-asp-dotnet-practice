# Data Model: Notification wording

> Written on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

No table, column, index or migration changed: the fix is in how the storefront words a notice and in what
the tests check. Stored notifications (`notifications` in Activity, specs/042) already carried the keys
the five broken kinds needed, so no backfill was needed.

## The declaration - `notification-kinds.json`

The one new artefact with a shape. It is a file embedded in `Ecommerce.Shared`, not a table:

```json
{
  "$comment": "What each notification kind carries in its data (specs/048). ...",
  "kinds": {
    "<Kind>": { "required": ["key", ...], "optional": ["key", ...] }
  }
}
```

`optional` may be absent (read as empty). At the merge it declared 16 kinds:

| Kind | Required | Optional |
| :-- | :-- | :-- |
| `OrderPaid` | `orderId`, `total`, `currency` | |
| `OrderFailed` | `orderId` | |
| `ParcelShipped` | `orderId`, `tracking` | `shop` |
| `OrderCancelled` | `orderId`, `by` | |
| `NewSale` | `orderId` | |
| `SaleCancelled` | `orderId` | |
| `ParcelReceived` | `orderId` | |
| `PayoutRecorded` | `amount`, `currency` | |
| `ModeratorGranted` | - | |
| `ModeratorRevoked` | - | |
| `ShopApproved` | `shop` | |
| `ShopRejected` | `shop`, `reason` | |
| `ProductApproved` | `product` | |
| `ProductRejected` | `product`, `reason` | |
| `ProductTakenDown` | `product`, `reason` | |
| `NewReview` | `product`, `rating` | |

## Rules `NotificationContract.Problems` applies

- A kind not in the file: `"<Kind>: not declared in notification-kinds.json"`.
- A required key missing from the data: `"<Kind>: missing \"<key>\""`.
- A key in the data that is neither required nor optional: `"<Kind>: sends \"<key>\", which is not
  declared"`.

`NotificationContract.KindsInCode` reads every `public const string` on `NotificationKind` by reflection;
the kinds-match test compares it with the declared set.

## Storefront strings

`client/src/locales/en/notifications.json`: `kind.NewReview` became `kind.NewReview_one` and
`kind.NewReview_other`. Vietnamese strings unchanged.

Later: specs/078 added a `placeholders` map to the same file (which data keys fill which placeholder), for
the administrator's rewording.
