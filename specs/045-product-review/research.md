# Phase 0 Research: Product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-24

D1 to D4 were recorded in [plan.md](./plan.md) when the feature was built; their text is kept here and
expanded. D5 to D10 are decisions the code makes that the original record did not write down. Where
the record does not say whether an alternative was weighed at the time, the alternatives below are the
ones the code's shape rules out, and they are marked as reconstructed.

---

## D1 - The status is text with a database default, not a new enum a rolled-back image cannot parse

**Decision**: `products.ReviewStatus` is `character varying(20)`, `NOT NULL`, default `'Approved'`,
mapped with `HasConversion<string>()`. The other four columns are nullable.

**Rationale**: An older image ignores the column. Pending products would then show during a rollback;
that is accepted and recorded. The default does two jobs at once: every product that existed before the
migration reads `Approved` with no data step (FR-003), and an older image that inserts a product without
knowing the column still writes a readable row. This is the project's standing rule for states added to
an existing row (decision 6 in `docs/project/decisions.md`: delivered, review status and shipments as
columns or text).

**Consequence to record**: an older image inserting a *seller's* product writes it `Approved`, so during
a rollback new seller listings go on sale unreviewed. Same cost as above, from the other direction.

**Alternatives considered**:

- **An integer enum column.** Rejected: a value added later is unreadable to an image that does not know
  it, and a number in the table says nothing to an operator.
- **A nullable column with a backfill.** Rejected: a data migration for a value a default gives for free,
  and a window in which existing products are neither approved nor pending.
- **A separate `product_reviews` table.** Rejected: every public read would need a join to answer a
  yes/no that belongs on the row; and "no row" would have to mean approved, which is the default by
  another name. (The name `product_reviews` went to customer ratings in specs/046.)

---

## D2 - Hide at the source

**Decision**: `Product.IsListed` (`ReviewStatus == Approved`) is asked in Catalog by the public listing
and search (`GetPaginatedAsync(listedOnly: true)`), the public lookup (`ProductReview.MaySee`), the
variant pricing path (`ProductVariant.Sellable`, used by `PriceVariants` and `DescribeVariants`) and the
product pricing path (`GetPrices` and `DescribeProducts`: `IsActive && IsListed`).

**Rationale**: Filtering listings, the lookup and pricing in Catalog covers the storefront, carts and
checkout together. A client-side filter would miss checkout. Because the gRPC answer already carried
`sellable` and Order already refused `sellable = false` with 409 (`Not currently for sale`), and Cart
already showed such a line as `NotForSale`, no service outside Catalog changed.

**Alternatives considered**:

- **Filter in the storefront.** Rejected: a request sent straight to the API, or a product already in a
  cart, would still be bought.
- **A new refusal in Order.** Rejected: a second place to get the same rule wrong, and Order would need
  to know about review, which is Catalog's fact.

---

## D3 - Resubmitting after an edit is automatic; after a rejection it is explicit

**Decision**: `ProductReview.AfterSellerEditAsync` moves an **approved** seller product to `Pending`
when its seller edits what a shopper reads; a **rejected** product stays rejected until its seller calls
`POST /api/products/{id}/resubmit`. A pending product edited stays pending and keeps its place in the
queue (`SubmittedAt` is not touched).

**Rationale**: An edit to an approved product is a change a moderator has not seen. A rejected product
is one the seller is still working on, so they say when it is ready. Decided with the user on
2026-09-24: what a shopper reads is what was approved; prices and stock are not reviewed.

**Alternatives considered** (reconstructed):

- **Every edit resubmits a rejected product.** Rejected: the seller fixing three things would put it in
  the queue after the first.
- **Edits to an approved product wait as a draft beside the live version.** Rejected: a second copy of
  every field a shopper reads, for a shop of this size.
- **Price changes go back to review too.** Rejected by the user.

---

## D4 - Variant photographs count as images

**Decision**: Uploading or removing a variant's photograph calls the edit hook, as the product's own
photograph does.

**Rationale**: The user said "images", and a shopper sees both - choosing a variant changes the
picture (specs/032).

**Alternatives considered**: product photograph only. Rejected for the reason above.

---

## D5 - A decision is one guarded UPDATE, with its audit entry and notice in the same transaction

**Decision**: `IProductRepository.TryReviewAsync(productId, from, to, reason, reviewedBy, at, stage)`
clears the change tracker, opens a transaction inside the execution strategy, runs
`UPDATE products SET ... WHERE "Id" = @id AND "ReviewStatus" IN (@from)` with `ExecuteUpdateAsync`, and
only if one row moved runs `stage` (audit entry, notice - staged through the outbox), saves once and
commits. Zero rows is `false`, which the handler turns into 409 after re-reading the product.

**Rationale**: FR-002 and Principle III. The `WHERE` is the decision: of two moderators approving at
once, one moves the row and the other matches nothing, so only the winner's audit entry and notice
exist. Moving to `Pending` stamps `SubmittedAt`; a decision stamps `ReviewedAt` and `ReviewedBy`. The
shape repeats specs/044's shop applications, so both queues behave the same.

**Alternatives considered** (reconstructed):

- **Load, check the status in code, save.** Rejected: two requests both see `Pending` and both write.
- **An optimistic concurrency token on `products`.** Rejected: every other writer of the product (prices,
  translations, images) would start failing with concurrency errors against the moderators.

---

## D6 - Who starts pending

**Decision**: `ProductReview.StartsPending(user)` is `Seller && !Admin`. `CreateProduct` sets
`ReviewStatus` and `SubmittedAt` from it.

**Rationale**: An administrator's product belongs to the shop itself (specs/027) and the shop does not
review itself. The edit hook excludes administrators the same way and ignores products with no seller,
so staff edits never send a product back (FR-004). A moderator cannot reach product writes at all
(`Seller,Admin` on those endpoints).

**Alternatives considered** (reconstructed): review everything, the shop's own included. Rejected by
the issue: "Products the shop itself lists are approved as listed."

---

## D7 - A hidden product is 404, the same as a missing one

**Decision**: `GetProductByIdQueryHandler` returns null - 404 - unless `ProductReview.MaySee`: listed, or
the caller is Admin, Moderator or the product's seller.

**Rationale**: The same reasoning as "not yours is 404, never 403" (specs/027): a 403 or a distinct answer
would confirm that an unlisted product exists at that id.

**Alternatives considered** (reconstructed): 403 for a hidden product. Rejected for the reason above.

---

## D8 - Take-down lands in `Rejected`, not a status of its own

**Decision**: Take-down moves `Approved → Rejected` with a reason; the seller resubmits it the same way as
a rejection. Only three states exist.

**Rationale**: To the seller the two are the same situation - not on sale, with a reason, fix it and send
it back - and a fourth value is one more a rolled-back image could meet (D1). The audit action
(`ProductTakenDown`) and the notice kind keep the difference visible.

**Alternatives considered** (reconstructed): a `TakenDown` status. Rejected: nothing would treat it
differently from `Rejected`.

---

## D9 - "My decisions" is the audit log, filtered

**Decision**: `GET /api/audit/mine` in Activity, `[Authorize(Roles = StaffRoles.Staff)]`, sends the
existing `GetAuditEntriesQuery(Category: "Moderation", ActorId: <token's id>)`.

**Rationale**: Every decision already writes a Moderation audit entry (specs/041), so the history exists;
the dashboard needs only the caller's own slice of it. The full audit log stays an administrator's.
`audit_entries` already had an index on (`ActorId`, `OccurredAt`).

**Alternatives considered** (reconstructed):

- **Open `GET /api/audit` to moderators.** Rejected: it would show them everybody's security and user
  entries.
- **Keep a decisions table in Catalog.** Rejected: a second record of what the audit log already holds,
  and shop decisions live in Identity, so it would cover half the dashboard.

---

## D10 - The dashboard counts from the queues themselves

**Decision**: `/admin/moderation` asks each queue for page 1 with page size 1 and shows its `totalCount`.
A moderator's console opens there (`AdminHome` redirects a non-administrator to `/admin/moderation`,
where specs/044 had sent them to `/admin/shops`).

**Rationale**: The count is the queue's own total, so the dashboard can never disagree with the queue it
links to, and no counting endpoint was needed.

**Alternatives considered** (reconstructed): a counts endpoint per service. Rejected: two more endpoints
whose numbers could drift from the lists.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Rollback to an image from before this feature | Pending products show publicly; new seller listings land `Approved` | Accepted and recorded (D1); the fix is rolling forward |
| Photograph addresses were not checked | A hidden product's image could be fetched by address | Closed by specs/081 (#166) |
| Some edits a shopper reads were not hooked | Option translations and new variants skipped review | Closed by specs/056 (#126); `CLAUDE.md` says a new edit of what a shopper reads must call the hook |
