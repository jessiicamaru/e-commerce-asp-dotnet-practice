# Phase 1 Data Model: Product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One table changed, in `ecommerce_catalog_db`: five columns and one index on `products`, added by
migration `20260923210256_AddProductReview`
(`server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/`). No table was added
anywhere, and no other service's schema changed.

---

## `products` - columns added

| Column | Type (PostgreSQL) | Null | Default | Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `ReviewStatus` | `character varying(20)` | not null | `'Approved'` | `Approved`, `Pending` or `Rejected`, stored as text (`HasConversion<string>()`, `HasMaxLength(20)`, `IsRequired()`) |
| `ReviewReason` | `character varying(500)` | null | - | Why it was rejected or taken down; the seller reads it. Cleared when it goes back to `Pending` |
| `SubmittedAt` | `timestamp with time zone` | null | - | When it last entered the queue: listed by a seller, resubmitted, or edited after approval |
| `ReviewedAt` | `timestamp with time zone` | null | - | When a member of staff last decided it |
| `ReviewedBy` | `uuid` | null | - | Who decided it - the user id from the token |

**Domain**: `Product.ReviewStatus` is the `ProductReviewStatus` enum (`Approved`, `Pending`, `Rejected`)
in `Ecommerce.Catalog.Domain/Entities/Product.cs`, defaulting to `Approved` in code as in the database.
`Product.IsListed => ReviewStatus == Approved` is computed and not mapped (`builder.Ignore(p => p.IsListed)`).

**Index**: `IX_products_ReviewStatus_SubmittedAt` on (`ReviewStatus`, `SubmittedAt`) - the moderators'
queue, pending and oldest submission first.

**No check constraint** was added on the three values; the column is written only through the enum
conversion. Nothing ties `ReviewReason` to `Rejected` in the database either: it is written by the same
statement that sets the status.

---

## Existing rows

The migration adds `ReviewStatus` with `defaultValue: "Approved"`, so every product listed before it
reads `Approved` - FR-003 without a data step. The comment in the migration says why: every product
already listed was on sale before review existed, and an older image that inserts without knowing the
column must not create something unreadable.

`SubmittedAt`, `ReviewedAt` and `ReviewedBy` stay null on those rows. An `Approved` product with a null
`ReviewedAt` therefore means "approved by default or listed by the shop", not "approved by someone".

---

## State transitions

```text
          seller lists                         admin lists
               │                                    │
               ▼                                    ▼
         ┌──────────┐   approve (Staff)      ┌──────────┐
  ┌────▶ │ Pending  │ ─────────────────────▶ │ Approved │
  │      └────┬─────┘                        └────┬─────┘
  │           │ reject (Staff, reason)            │  take-down (Staff, reason)
  │           ▼                                   │  seller edits words/photos ──▶ Pending
  │      ┌──────────┐ ◀───────────────────────────┘
  └───── │ Rejected │
resubmit └──────────┘
(Seller, Admin)
```

| Transition | Guard (`WHERE "ReviewStatus" IN`) | Writes | Audit action | Notice to the seller |
| :--- | :--- | :--- | :--- | :--- |
| create → `Pending` | - (insert) | `SubmittedAt = now` | `ProductCreated` (category Catalog, existing) | - |
| create → `Approved` | - (insert) | - | `ProductCreated` (category Catalog, existing) | - |
| `Pending` → `Approved` | `Pending` | `ReviewReason = null`, `ReviewedAt`, `ReviewedBy` | `ProductApproved` | `ProductApproved` |
| `Pending` → `Rejected` | `Pending` | `ReviewReason`, `ReviewedAt`, `ReviewedBy` | `ProductRejected` | `ProductRejected` (+ `reason`) |
| `Approved` → `Rejected` | `Approved` | `ReviewReason`, `ReviewedAt`, `ReviewedBy` | `ProductTakenDown` | `ProductTakenDown` (+ `reason`) |
| `Rejected` → `Pending` | `Rejected` | `ReviewReason = null`, `SubmittedAt = now` | `ProductResubmitted` | - |
| `Approved` → `Pending` | none: tracked-entity change in the edit's own save | `ReviewReason = null`, `SubmittedAt = now` | `ProductSentForReview` | - |

The review audit entries (every action but `ProductCreated`) are category `Moderation`. A decision on a product with no seller (the shop's own)
writes the audit entry and no notice. Every guarded transition is written by `TryReviewAsync` in one
transaction with what it stages; zero rows moved means nothing is staged and the caller gets 409.

The edit transition happens only when the product is `Approved`, has a seller, and the caller is not an
administrator (`ProductReview.AfterSellerEditAsync`). At merge it was called from six handlers: set and
remove a product translation, upload and remove the product image, upload and remove a variant image.
Specs/056 (#126) added two more.

`TryReviewAsync` does not touch `UpdatedAt`. The history tabs order by `ReviewedAt`, falling back to
`UpdatedAt` for rows that were never decided.

---

## Reads that changed

| Read | Filter added |
| :--- | :--- |
| `GetPaginatedAsync` (public listing, search, category) | `listedOnly = true` by default → `WHERE "ReviewStatus" = 'Approved'`; `GetMyProducts` passes `false` |
| `GetForReviewAsync` (new) | `WHERE "ReviewStatus" = @status`; Pending ordered by `SubmittedAt`, `Id`; the others by `COALESCE("ReviewedAt", "UpdatedAt") DESC`, `Id` |
| `GetProductById` | none in SQL; the handler returns null unless `MaySee` |
| gRPC `GetPrices`, `DescribeProducts` | `Sellable = IsActive && IsListed` |
| gRPC `PriceVariants`, `DescribeVariants` | through `ProductVariant.Sellable = IsActive && (Product is null \|\| (Product.IsActive && Product.IsListed))` |

---

## What did not change, and why

- **No new enum value on an existing column, no dropped or renamed column.** All five columns are
  additive (four nullable, one with a default), so the previous Catalog image still runs against the
  new schema - the constitution's Schema evolution rule. The `schema-compatibility` job has nothing to
  flag.
- **Activity's `audit_entries`** was not changed: its index on (`ActorId`, `OccurredAt`), from its
  initial migration (specs/041), already serves `GET /api/audit/mine`.
- **`notifications`** was not changed: a notice stores a kind and data, so three new kinds needed no
  column.
- **Order, Cart, Inventory**: no schema change. They learn about review only through `sellable`.

## Rollback

`Down` drops the index and the five columns. Redeploying the previous image without running `Down` is
the intended rollback and works, at the cost recorded in research D1.
