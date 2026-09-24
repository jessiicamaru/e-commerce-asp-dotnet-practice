# Implementation Plan: The notices nobody got

**Branch**: `059-missing-notices` | **Spec**: [spec.md](spec.md) | **Issue**: #128 (part B)

## Design

- **`NotificationKind` and `notification-kinds.json`**, four new kinds and their keys:

  | Kind | Required keys |
  | :-- | :-- |
  | `AccountLocked` | `until`, `reason` |
  | `AccountBanned` | `reason` |
  | `ParcelAutoDelivered` | `orderId` |
  | `ReviewHidden` | `product`, `reason` |

- **Identity:** the lock and ban handlers notify before their one save. `until` is ISO 8601 UTC, and the
  storefront words it.
- **Order:** `AutoConfirmDeliveriesCommandHandler`'s stage reads each delivered parcel's order facts, and
  `OrderNotices.AutoDeliveredAsync` tells that parcel's seller. It is inside the sweep's transaction, like
  its audit entry and `ParcelDeliveredEvent`.
- **Catalog:** the hide stage (specs/057) also notifies the author.
- **Storefront:**
  - `describeNotification(t, n, language)` formats `until` with `toLocaleString(language)`;
  - both callers pass `i18n.language`;
  - wording goes in `locales/{en,vi}/notifications.json`.

## Constitution check

- III (atomic writes): every notice is staged in the transaction of the change it announces. Pass.
- V (evidence): each notice has a server test that fails first. The storefront contract test fails first
  on the four kinds it has no words for. Pass.
