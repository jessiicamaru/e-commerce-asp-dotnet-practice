# Phase 1 Data Model: Shop applications

> Written on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_identity_db` (host port 5435), added by migration
`20260923204529_AddShopApplications`. Mapping: `ShopApplicationConfiguration` in
`server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/`. No other service's
schema changed.

---

## `shop_applications`

One row per application. A person may have many over time; at most one is `Pending`.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK (`PK_shop_applications`), `ValueGeneratedNever()` | Minted in code as `Guid.CreateVersion7()` (ADR-001) |
| `UserId` | uuid | Not null, FK `FK_shop_applications_users_UserId` → `users.Id`, `ON DELETE CASCADE` | The applicant. From the token, or the account `register-seller` just created |
| `ShopName` | character varying(100) | Not null | The name the shop will trade under; copied to `seller_profiles.ShopName` on approval |
| `Description` | character varying(1000) | Nullable | What they mean to sell, in their words - what a moderator decides on. Blank is stored as null |
| `Phone` | character varying(20) | Nullable | A contact phone. Blank is stored as null |
| `Status` | character varying(20) | Not null | `Pending`, `Approved` or `Rejected`, as text (`HasConversion<string>()`) |
| `DecisionReason` | character varying(500) | Nullable | Why it was rejected; null for an approval and while pending |
| `DecidedBy` | uuid | Nullable, no FK | The staff member who decided, from their token |
| `DecidedAt` | timestamp with time zone | Nullable | When it was decided |
| `CreatedAt` | timestamp with time zone | Not null | When it was sent; the queue's order |

**Indexes**:

- `IX_shop_applications_one_pending` - **unique** on `UserId`, filter `"Status" = 'Pending'`. FR-001: the
  database refuses a second pending application whatever raced (research D4). The apply handler
  recognises this index's name in a violation and answers 409.
- `IX_shop_applications_Status_CreatedAt` on `(Status, CreatedAt)` - the queue: pending, oldest first
  (research D11).

**Why `DecidedBy` has no foreign key**: not recorded. It holds a staff member's id and is read by nobody
at the merge; the audit entry for the decision names the actor as well.

**Why `ON DELETE CASCADE` on `UserId`**: an application belongs to its account, and `seller_profiles`
from specs/027 already cascades the same way.

---

## State transitions

```text
                  apply / register-seller
                           │
                           ▼
                     ┌──────────┐
                     │ Pending  │
                     └────┬─────┘
             approve      │      reject (reason required)
          ┌───────────────┴───────────────┐
          ▼                               ▼
     ┌──────────┐                   ┌──────────┐
     │ Approved │                   │ Rejected │ ──▶ the person may send a NEW application
     └──────────┘                   └──────────┘
```

`Pending` is the only state with outgoing transitions. Both are one guarded statement in
`ShopApplicationRepository.TryDecideAsync`:

```sql
UPDATE shop_applications
SET "Status" = @decision, "DecisionReason" = @reason, "DecidedBy" = @decidedBy, "DecidedAt" = @decidedAt
WHERE "Id" = @id AND "Status" = 'Pending';
```

(written as `ExecuteUpdateAsync`), so a second decision affects zero rows and answers 409 (FR-002).

| Transition | Also written in the same transaction | Trigger |
| :--- | :--- | :--- |
| → `Pending` | For `register-seller`: the `users` row with `Customer`, its refresh token and the `ShopApplied` audit outbox row. For apply: the `ShopApplied` audit outbox row | `POST /api/auth/register-seller`, `POST /api/shop-applications` |
| `Pending` → `Approved` | `user_roles` row for `Seller` (unless already held), `users.UpdatedAt`, a `seller_profiles` row, outbox rows for `SellerRegisteredEvent`, `AuditEntryRecorded` (`ShopApproved`) and `UserNotificationRequested` (`ShopApproved`) | `POST /api/shop-applications/{id}/approve` |
| `Pending` → `Rejected` | Outbox rows for `AuditEntryRecorded` (`ShopRejected`) and `UserNotificationRequested` (`ShopRejected`) | `POST /api/shop-applications/{id}/reject` |

---

## Tables touched but not changed in shape

| Table | What changes | Why no schema change |
| :--- | :--- | :--- |
| `users`, `user_roles`, `roles` | Registration through `register-seller` now links `Customer` only; approval links `Seller` | Same rows, written at a different moment |
| `seller_profiles` (specs/027) | Now inserted only by an approval, never by registration | Its primary key is `UserId`, which is what "one shop per account" rests on; research D2 explains why the guard, not this key, is the concurrency control |
| `OutboxMessage`, `OutboxState` (Identity's transactional outbox, specs/027) | Carry the approval's three messages | Already present since `20260922173126_AddSellerProfilesAndOutbox` |
| Catalog `sellers` (read model, specs/027) | A row appears when the approval's `SellerRegisteredEvent` arrives, instead of at registration | Consumer and table unchanged |

---

## What did not change, and older images

The migration only **adds** a table (expand). Nothing was dropped, renamed or narrowed, so the
constitution's schema-evolution rule is met and no `schema-compatibility` comment applies.

What a rollback to an image from before this feature would mean, stated because the behaviour differs
even though the schema is compatible: the older `register-seller` would again grant `Seller` and open a
shop at once, and any `Pending` rows would sit with nobody able to decide them - the older image has no
endpoint that reads `shop_applications`. Nothing would fail; the rows would simply be ignored.

No backfill (research D3): the table starts empty, and shops that existed before it keep their role and
profile with no application row.
