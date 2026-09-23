# Implementation Plan: Product review before sale

**Branch**: `045-product-review` | **Spec**: [spec.md](spec.md)

## Technical Context

**Catalog: data**
- `products` gains five columns: `ReviewStatus` (text: `Approved`, `Pending` or `Rejected`),
  `ReviewReason`, `SubmittedAt`, `ReviewedAt` and `ReviewedBy`, with an index on
  (`ReviewStatus`, `SubmittedAt`).
- The migration sets the column default to `'Approved'`. Existing rows are therefore approved, and an
  older image that inserts a product without knowing the column still writes a readable row.

**Catalog: who sees and sells what**
- `Product.IsListed` means `Approved`.
- `ProductVariant.Sellable` requires it, and both pricing paths use it. Checkout therefore refuses an
  unapproved product through the path that already refuses inactive ones.
- `GetPaginatedAsync(listedOnly = true)` filters public listings and search. A seller's own list passes
  `false`.
- `GetProductById` returns null (404) unless the product is listed or the caller is its seller or staff.

**Catalog: review decisions**
- `CreateProduct` starts a seller's product as `Pending`. An administrator's product starts `Approved`.
- `ProductReviewHandlers` covers the queue, approve, reject, take-down and resubmit.
- `IProductRepository.TryReviewAsync` runs a guarded `UPDATE ... WHERE "ReviewStatus" IN (...)`, then a
  stage that writes the audit entry and the notification, all in one transaction.
- Endpoints: `GET /api/products/review` and `POST {id}/approve | reject | take-down` for Staff;
  `POST {id}/resubmit` for Seller or Admin.

**Catalog: edits that send a product back**
- `ProductReview.AfterSellerEditAsync` runs before the one save in six handlers:
  - setting or removing a product translation (the name and description);
  - uploading or removing the product's image;
  - uploading or removing a variant's image.

**Activity**
- `GET /api/audit/mine` (Staff) returns the caller's Moderation entries.

**Shared**
- `NotificationKind.ProductApproved`, `ProductRejected` and `ProductTakenDown`.

**Client**
- `services/moderation` and `hooks/moderation`.
- `/admin/products` is the review queue, with tabs per status and a reason dialog.
- `/admin/moderation` is the dashboard. A moderator's console opens on it.
- Sellers see a `ReviewBadge` in their list and a `ReviewBanner` on the product page (the reason, and
  "send back for review").
- The notification wording covers the three new kinds.

## Research

- **D1 - The status is text with a database default, not a new enum a rolled-back image cannot parse.**
  An older image ignores the column. Pending products would then show during a rollback; that is
  accepted and recorded.
- **D2 - Hide at the source.** Filtering listings, the lookup and pricing in Catalog covers the
  storefront, carts and checkout together. A client-side filter would miss checkout.
- **D3 - Resubmitting after an edit is automatic; after a rejection it is explicit.** An edit to an
  approved product is a change a moderator has not seen. A rejected product is one the seller is still
  working on, so they say when it is ready.
- **D4 - Variant photographs count as images.** The user said "images", and a shopper sees both.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Catalog owns products and their review. Activity serves the audit read. |
| III | Every decision commits with its audit entry and notification. The guarded update makes a second decision a no-op. |
| IV | The reviewer comes from the token. Resubmit goes through `SellerOwnership`. |
| V | 7 integration tests, 3 mutation checks, Bruno for the round trip, and client tests for the queue, dashboard and banner. |
