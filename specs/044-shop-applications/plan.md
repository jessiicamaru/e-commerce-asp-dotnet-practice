# Implementation Plan: Shop applications

**Branch**: `044-shop-applications` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Identity domain.** `ShopApplication` is stored in `shop_applications`:
  - Fields: id, user, shop name, description, phone, status (string), decision reason, decided by,
    decided at, created.
  - A partial unique index on `UserId WHERE "Status" = 'Pending'`.
  - An index on (status, created) for the queue.
- **`register-seller`** now creates the account with Customer only, plus a pending application, in one
  save. It no longer creates a `SellerProfile` and no longer publishes `SellerRegisteredEvent`.
- **`ShopApplicationHandlers`**:
  - Apply: Customer only; the applicant comes from the token.
  - Mine: the caller's own applications.
  - Queue: Staff.
  - Approve and reject: Staff.
  - `ShopApplicationRules.EnsureMayApplyAsync` is shared, so the two ways of applying cannot drift.
- **Deciding.** `IShopApplicationRepository.TryDecideAsync` runs a guarded
  `UPDATE ... WHERE "Status" = 'Pending'` in its own transaction, then calls the handler's `stage`.
  - For an approval, the stage adds the Seller role and the `SellerProfile`, publishes
    `SellerRegisteredEvent`, and records the audit entry and the notification.
  - For a rejection, the stage records the audit entry and the notification.
  - Only the winner of the guarded update runs its stage.
- **Shared**: `NotificationKind.ShopApproved` and `ShopRejected`.
- **Gateway**: `/api/shop-applications/**` → identity.
- **Client**:
  - `/open-shop` shows the form and the history. When the latest application is approved,
    "Go to my shop" renews the session first, through `refreshSession`, which is new on `AuthState`.
  - The user menu offers "Open a shop" to anybody who does not sell yet.
  - `/admin/shops` is the queue, with a tab per status.
  - A moderator's console now opens on `/admin/shops`.

## Research

- **D1 - An application, not a flag on the user.** A rejected application stays, with its reason, and
  the person can apply again. The history is what staff decide from.
- **D2 - The decision guard is the concurrency control.** The shop's key is the user id, so a second
  approval would already fail on the insert. It would fail with a 500, though, and only after
  publishing. The guarded update stops the second approval before anything else is written.
- **D3 - No backfill.** Today's shops have a profile and the Seller role. Nothing reads an application
  to decide what somebody may do, so existing shops need no rows.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Identity owns accounts, roles and applications; Catalog learns of an approved shop through the event it already consumes. |
| III | The decision, the role, the shop, the event, the audit entry and the notification commit in one transaction. The guarded update makes a second decision a no-op. |
| IV | The applicant comes from the token. A decision is a staff action on an application id in the route. |
| V | Integration tests cover the role, the event, the race and the rules. Mutation checks cover the guard and the form. Bruno covers the round trip. |
