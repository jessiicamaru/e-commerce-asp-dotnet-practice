# Phase 0 Research: Shop applications

> Written on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

D1 to D3 are the decisions the original plan recorded, kept word for word and expanded. D4 onwards are
decisions the code at the merge makes and the original record did not write down; where the record does
not say who made one or why, that is stated.

---

## D1 - An application, not a flag on the user

**Decision**: A shop starts as a row in its own table, `shop_applications`, with a status of `Pending`,
`Approved` or `Rejected`. The role and the shop are created only when an application is approved.

**Rationale**: A rejected application stays, with its reason, and the person can apply again. The history
is what staff decide from.

A flag on the user ("wants to sell", "seller pending") holds one state at a time. It cannot say that this
person was refused last month for listing nothing but a phone number, or that this is their third try,
and the reason for a rejection would have nowhere to live. A row per application also gives the decision
somewhere to record who made it and when (`DecidedBy`, `DecidedAt`).

**Alternatives considered**:

- **A status column on `users` or `seller_profiles`.** Rejected: it loses the history on every re-apply,
  and a `seller_profiles` row for somebody who is not a seller would make "has a profile" stop meaning
  "is a seller", which specs/027's `/api/sellers/me` and Catalog's read model both rely on.
- **Keep granting `Seller` at registration and gate listing behind a separate approval.** Rejected: every
  seller write in Catalog, Inventory and Order already asks for the Seller role, so gating would mean a
  second check in every one of them. Withholding the role closes all of them at once.

---

## D2 - The decision guard is the concurrency control

**Decision**: `IShopApplicationRepository.TryDecideAsync` opens a transaction, runs one guarded
`UPDATE shop_applications SET "Status" = @decision, ... WHERE "Id" = @id AND "Status" = 'Pending'`, and
only if that statement changed a row does it run the handler's `stage` callback, call
`SaveChangesAsync` and commit. A caller that changed no row gets `false`, and the handler answers 409.

**Rationale**: The shop's key is the user id, so a second approval would already fail on the insert. It
would fail with a 500, though, and only after publishing. The guarded update stops the second approval
before anything else is written.

How it serialises: the first `UPDATE` takes the application row's lock and holds it until its transaction
commits. A second decision on the same row waits for that lock, and under PostgreSQL's default READ
COMMITTED isolation re-evaluates its `WHERE` against the committed row, finds `Status` no longer
`Pending`, and changes nothing. No explicit `SELECT ... FOR UPDATE` is needed, because the update is the
lock. The transaction runs inside `CreateExecutionStrategy().ExecuteAsync(...)` and clears the change
tracker first, so a retry starts clean.

What "everything the decision does" means for an approval: the `user_roles` row for `Seller`, the
`seller_profiles` row, `SellerRegisteredEvent`, the audit entry and the notification - all staged by the
callback and saved by the one `SaveChangesAsync` inside the transaction that holds the decision, the
outbox rows with them (constitution III).

**Alternatives considered**:

- **Rely on the `seller_profiles` primary key and catch the violation.** Rejected, for the reason above: a
  500 instead of a 409, and the losing request has already staged its event, audit entry and notice by
  the time the insert fails.
- **Read the application, check `Status == Pending` in code, then save.** Rejected: two requests both read
  `Pending` and both proceed. The check has to be the write.
- **Optimistic concurrency (a row version) with a retry.** Rejected: the right outcome for the loser is
  not a retry but a refusal, and a guarded update gives exactly that in one statement.

---

## D3 - No backfill

**Decision**: The migration creates an empty table. Shops that exist before this feature get no
application row.

**Rationale**: Today's shops have a profile and the Seller role. Nothing reads an application to decide
what somebody may do, so existing shops need no rows.

The issue asked for existing shops to be "approved as they are". They are: they keep the role and the
profile, which is all approval ever produces. An `Approved` row invented for them would carry a
`DecidedBy` and a `DecidedAt` nobody gave.

**Alternatives considered**:

- **Insert an `Approved` application for every existing `seller_profiles` row.** Rejected: invented
  history, and nothing would read it.

---

## D4 - One pending application per person is a partial unique index

**Decision**: `IX_shop_applications_one_pending`, a unique index on `UserId` filtered by
`"Status" = 'Pending'`. The apply handler checks first (`ShopApplicationRules.EnsureMayApplyAsync`) to
give a clear 409, and also catches the index violation on save - recognised by the index's name in the
inner exception - and turns it into the same 409.

**Rationale**: FR-001 says "enforced by the database". Two tabs submitting at once both pass the code
check; only the index stops the second. Filtering on `Pending` is what lets a person apply again after a
rejection and keep the rejected row as history.

**Alternatives considered**:

- **The code check alone.** Rejected: the race above.
- **A plain unique index on `UserId`.** Rejected: it forbids re-applying, which US2 requires.
- **Let the violation surface.** Rejected: an unhandled `DbUpdateException` is a 500 from the shared
  exception handler; the person did nothing a 409 would not describe.

---

## D5 - `register-seller` keeps its address and changes its meaning

**Decision**: `POST /api/auth/register-seller` stays, still takes a shop name, and gains optional
`description` and `phone`. It now creates the account with `Customer` only plus a `Pending`
application, in one save, and no longer creates a `SellerProfile` or publishes `SellerRegisteredEvent`.

**Rationale**: The issue asked for it to keep working for a new person. The one-step path is what the
Bruno seller folder and the local demo seeder already used; keeping the
address and changing what it grants closes the hole without breaking them. (The pull request records
that the seeder, `local/seed-demo.py`, is gitignored and was changed to approve its demo sellers'
applications.) Registration also became one
save where it had been two - the account, the application, the audit entry and the session now commit
together.

The shared rule is not called here. `EnsureMayApplyAsync` refuses a seller and somebody already waiting;
neither can be true of an account this request is about to create, and an email already in use is
refused before anything else (409 `An account with this email already exists.`). **Correction to the
original plan**, which said the rule "is shared, so the two ways of applying cannot drift": at the merge
only `ApplyForShopCommand`'s handler calls it (`git grep EnsureMayApply` finds one caller), and the
doc comment on `ShopApplicationRules` says the same thing the plan did. The pull request's "the two ways
of applying share one rule" is the same claim. The behaviour is not affected; the sentence was.

**Alternatives considered**:

- **Remove `register-seller`; everybody registers, then applies.** Not recorded as considered. It would
  have broken the scripts above for no gain in safety, since the endpoint no longer grants anything.
- **Keep it granting `Seller`.** Rejected: that is the defect.

---

## D6 - Who decides: a moderator or an administrator

**Decision**: The queue, approve and reject are `[Authorize(Roles = StaffRoles.Staff)]` - Admin or
Moderator.

**Rationale**: Decided with the user, as the checklist records. Moderators exist since specs/043 and had
no queue of their own; the console now opens on this one for them.

**Alternatives considered**:

- **Administrators only.** Not recorded as considered beyond the question put to the user.
- **Automatic approval with a later review.** Not recorded; it would reopen the hole for as long as the
  review took.

---

## D7 - The new role reaches the session at its next renewal

**Decision**: Approval changes the database only. The applicant's existing access token keeps the roles
it was issued with; the next sign-in or refresh carries `Seller`. The storefront adds `refreshSession()`
to `AuthState`, and `/open-shop` calls it before navigating to `/shop`.

**Rationale**: An access token is a signed statement made at issue time; nothing can add a role to it
afterwards. The refresh path already re-reads the user's roles, so renewing is enough, and it keeps the
applicant signed in. The storefront reads roles from the authentication response, not by decoding the
token (specs/028), so the renewed response is what redraws the menu.

**Alternatives considered**:

- **Tell the applicant to sign out and in.** Rejected: it works, and it looks like a bug to the one
  person the approval just helped.
- **Revoke the applicant's sessions on approval.** Not recorded as considered. There was no mechanism to
  refuse an access token early until specs/065, and it would sign them out rather than help them in.

---

## D8 - A rejection needs a reason, and the applicant reads it

**Decision**: `RejectShopApplicationCommand.Reason` is required (not blank, at most 500 characters),
stored in `DecisionReason`, returned on the applicant's own list, and carried in the `ShopRejected`
notice's data. `/admin/shops` will not send a rejection with an empty reason.

**Rationale**: The applicant may apply again (D1), and a refusal with no reason gives them nothing to
change. Approval takes no reason; there is nothing to explain.

**Alternatives considered**:

- **Optional reason.** Not recorded as considered; the issue asked for "with a reason".

---

## D9 - One response record, two views of it

**Decision**: `ShopApplicationResponse` has `ApplicantEmail` and `ApplicantName`. `ShopApplicationResponse.Mine`
leaves them null; `ShopApplicationResponse.ForStaff` fills them from a join to `users`. The applicant's
own list uses `Mine`; the queue and the decision responses use `ForStaff`.

**Rationale**: FR-003. The applicant knows who they are; the fields matter to staff, who decide on a
person as well as a shop name. One record keeps the storefront's type single.

**Alternatives considered**:

- **Two response types.** Not recorded as considered.

---

## D10 - Where each event lands in the audit log and the inbox

**Decision**: Applying is recorded as `ShopApplied` under **User** (subject `ShopApplication`), from both
paths - `register-seller` records it with the new account as the actor (`AuditActors.Of(user)`), since
the request carries no token. Approving and rejecting are `ShopApproved` and `ShopRejected` under
**Moderation**, with the before and after status (and the resulting roles, or the reason). The applicant
is notified through two new kinds, `ShopApproved` (data `shop`, link `/shop`) and `ShopRejected` (data
`shop` and `reason`, link `/open-shop`), in `NotificationKind`.

**Rationale**: The issue asked for Moderation audit entries for every decision and notices to the
applicant. Applying is the person's own act, so it sits with other account events. Registration used to
record `ShopOpened` on subject `User`; that action no longer happens at registration.

**Alternatives considered**: none recorded.

---

## D11 - The queue is oldest first; history is newest first

**Decision**: `GetPageAsync` orders `Pending` by `CreatedAt` ascending, anything else (a decided status, or
no status filter) by `CreatedAt` descending, `Id` breaking ties. The order is applied after the join to
`users`.

**Rationale**: "So nobody waits behind people who applied after them", in the repository's words. The
ordering sits after the join because, as the code notes, an order inside a subquery that is then joined
is not kept. An index on `(Status, CreatedAt)` serves the queue.

**Alternatives considered**: none recorded.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| A member of staff decides their own application | A moderator also holds `Customer` and may apply; nothing in the approve handler compares the decider with the applicant | Not addressed at the merge, and the record does not say whether it was considered. The decision is audited under Moderation with the actor, so it is visible afterwards |
| The applicant's email is not known to be theirs | A shop is a public claim in an address's name | Closed later by specs/063: applying needs a confirmed address, and approving a `register-seller` application is 409 until the applicant confirms |
| An approval with the broker down | Catalog shows the new seller's products as the shop's own until it hears | The event is in Identity's transactional outbox and is delivered when the broker returns; Catalog's read model is display data (specs/027) |
