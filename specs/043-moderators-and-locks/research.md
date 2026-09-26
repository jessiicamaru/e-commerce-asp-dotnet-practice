# Phase 0 Research: Moderators, locks and bans

> Written on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-24

D1 to D3 were recorded in the original plan and are kept word for word, then expanded. D4 onwards are
reconstructed from the code at the merge, its comments, the pull request and
[decisions 26 and 27](../../docs/project/decisions.md). Where the record does not say what else was weighed,
the alternatives below are the ones the code visibly declines, and that is said.

---

## D1 - Lock and ban are two columns, not a status enum

**Decision**: A ban and a lock can overlap, and an older image must still parse the row. Two nullable pairs
answer "why" and "until when" without either problem. `users` gains `LockedUntil` / `LockReason` and
`BannedAt` / `BanReason`; `User.IsLocked(now)` is `LockedUntil > now` and `IsBanned` is
`BannedAt is not null`.

**Rationale**: a lock is a date in the future; a ban is an open-ended fact. Both need a reason the person
reads. Kept as two pairs, they can both hold at once (a person locked for spam and then banned for fraud),
unlocking cannot silently lift a ban, and a lock that has run out needs no job to clear it - comparing with
`now` is enough. Adding nullable columns is the "expand" half of expand-then-contract: an Identity image
from before this migration reads `users` exactly as it did, which the constitution's schema-evolution rule
requires.

**Alternatives considered**:

- **An `AccountStatus` enum column (`Active`, `Locked`, `Banned`).** Rejected: it cannot say "locked and
  banned", it still needs a date and two reasons beside it, and a value added later stops an older image
  from parsing the row - the trap Order avoided the same way for `Delivered` (specs/040).
- **Reusing the existing `IsActive` column.** Rejected: it holds no end date and no reason, and nothing
  reads it. It is still unread after this feature.
- **ASP.NET Core Identity's lockout fields.** Not applicable: this service does not use ASP.NET Core
  Identity's user store.

---

## D2 - 403 at sign-in only after the right password

**Decision**: Before the password is checked, a locked account and a wrong password must look the same
(the #28 rule). After it, the person is who they say they are and deserves the reason. `LoginCommandHandler`
checks the password first; only then does a stopped account get `ForbiddenException` with "This account is
banned: {reason}" or "This account is locked until {yyyy-MM-dd HH:mm} UTC: {reason}", and a `SignInRefused`
entry under Security.

**Rationale**: #28 made an unknown email and a wrong password one 401 with one message, so the answer
cannot be used to learn which emails have accounts. Refusing a locked account before the password would
reopen that: a stranger typing any password would learn the account exists and that it is locked, and why.
After the right password there is nothing left to hide from the caller - they are the account's owner -
and telling them the reason and end date is what stops them retrying and assuming the shop is broken.
Recorded as [decision 27](../../docs/project/decisions.md).

**Alternatives considered**:

- **Refuse before the password, with a generic 401.** Rejected: safe, but the owner of a locked account
  never learns why, and cannot tell a lock from a mistyped password.
- **Refuse before the password, with the reason.** Rejected: account enumeration, the defect #28 fixed.
- **Refuse with 401 and the reason after the password.** Rejected: 401 means "who are you?", and the
  storefront treats every 401 at sign-in as "wrong email or password". The caller is known and the answer
  is no, which is 403.

---

## D3 - Moderator is the only grantable role

**Decision**: Admin would make a second bootstrap path. Seller has its own path (a shop application, #89).
Customer is everybody. `GrantRoleCommandValidator` and `RevokeRoleCommandValidator` accept only
`RoleNames.Moderator` and say so in words; the route still carries `{role}` so a later role needs no new
address.

**Rationale**: the first administrator comes from the environment, once, and `DataInitializer` closes that
path as soon as any administrator exists. An endpoint that grants `Admin` would be a second path, reachable
by any administrator's stolen token, that the bootstrap's closing was meant to prevent. `Seller` carries a
shop profile and (from specs/044) an approval; granting the bare role would make a seller with no shop.
Everybody who registers is already a `Customer`. Recorded as
[decision 26](../../docs/project/decisions.md).

**Alternatives considered**:

- **Any role, administrators only.** Rejected for the reasons above; it also makes "who is an
  administrator" a question the audit log alone can answer.
- **A dedicated `/moderator` endpoint with no role in the path.** Not recorded as considered. The
  `{role}` segment keeps the address general while the validator keeps the behaviour narrow.

---

## D4 - Rules about the target live in the handler, in `ModerationRules`

**Decision**: The controller's attributes decide who reaches an endpoint (`Staff` for the class, `Admin`
for grant, revoke, ban and lift). `ModerationRules.EnsureMayStop(caller, target)` decides the rest against
the stored target row: nobody stops themselves or an administrator, and a moderator does not stop another
moderator. It is called by both the lock and the ban handler.

**Rationale**: an attribute runs before the target is read and cannot know who it is. The rule "not an
administrator" depends on the target's roles in the database, not on anything in the request, so it has to
run after the row is loaded - and FR-007 requires it to hold whatever the client sends. One static class
named for the subject keeps the rule in one place for both handlers, and gives specs/050 an obvious home
for the matching unlock rule (`EnsureMayRelease`).

**Alternatives considered**:

- **An authorization policy with a resource handler (`IAuthorizationHandler` over the user).** Not recorded
  as considered. It would move a business rule into WebApi wiring and still need the row loaded first.
- **Trusting the client, which already hides these actions.** Rejected by FR-007 and by the storefront's
  own rule: a role on the response is for drawing, never for deciding.

---

## D5 - A stop ends sessions by revoking refresh tokens, and refresh reads the row

**Decision**: A lock or a ban saves the account and its audit entry, then calls
`RevokeAllRefreshTokensAsync(user.Id, now)`. Independently, `RefreshTokenCommandHandler` refuses a locked
or banned account with the ordinary 401 whatever token it presents. An access token already issued lives
out its minutes.

**Rationale**: the refresh token is the only server-side handle on a session, so revoking all of them ends
every session at the next refresh. The revocation is a bulk `ExecuteUpdate` that runs after the save, so it
is not in the change's transaction; the refresh check is what makes that safe. If the revocation failed, or
a session were created in between, refresh would still refuse it, because "the account row decides, not the
token" (the handler's comment). Access tokens are validated by every service from the signature alone, so
stopping one mid-life would need a lookup on every request in every service - rejected in the spec's Out of
scope, and later solved another way by specs/065 (an in-memory revocation list fed by a message).

**Alternatives considered**:

- **A lookup of the account on every request in every service.** Rejected: every service would depend on
  Identity (or a copy of its data) to answer any request.
- **Rely on the revocation alone, without the refresh check.** Rejected: one missed row, or a refresh racing
  the lock, would leave a stopped person signed in for up to the refresh token's 7 days.
- **Shorter access tokens.** Not recorded as considered; the 15-minute lifetime was left as it was.

---

## D6 - A moderator locks for at most 30 days; an administrator for up to 365; only an administrator bans

**Decision**: `LockUserCommandValidator` allows 1 to `ModerationRules.AdminMaxLockDays` (365) days for
anyone. The handler refuses a caller who is not an administrator asking for more than
`ModeratorMaxLockDays` (30) with `ForbiddenException`, **before** reading the target. Ban and lift are
`[Authorize(Roles = RoleNames.Admin)]`.

**Rationale**: decided with the user on 2026-09-24 (issue #88): moderators share the day-to-day work and may
cool an abusive account off; a ban, open-ended, is an administrator's call, as is stopping another member of
staff. The 30-day cap depends on the caller's role and on a number in the body, so it cannot be an
attribute; the validator holds the outer bound that applies to everybody. Checking the cap before the
lookup means a moderator asking for 31 days learns nothing about whether the id exists.

**Alternatives considered**:

- **Moderators may not lock at all.** Rejected by the user's decision: the point of the role is to share
  the work.
- **Moderators may ban.** Rejected by the same decision.
- **An end date in the request instead of a number of days.** Not recorded as considered. Days make the
  cap a comparison of two integers and leave the clock to the server.

---

## D7 - One name for Staff, shared by every service

**Decision**: `Ecommerce.Shared.Authentication.StaffRoles` with `Admin`, `Moderator` and
`Staff = Admin + "," + Moderator`, used as `[Authorize(Roles = StaffRoles.Staff)]`.

**Rationale**: the comma is ASP.NET Core's "any of", so a constant string needs no policy registration in
any service. The moderation endpoints that follow live in Identity and Catalog; the same list typed in each
would drift (rule 1 in the feature page). Identity's own `RoleNames` stays the source of the role names
Identity seeds; the shared constants repeat two of them as strings because Shared cannot reference a
service's Domain.

**Alternatives considered**:

- **A named authorization policy registered by `AddJwtAuthentication`.** Not recorded as considered. It
  would work, but a string constant is visible at the attribute and needs no registration to go wrong.
- **Each service lists the roles itself.** Rejected: that is the drift the constant exists to prevent.

---

## D8 - `ForbiddenException`: a 403 whose message is shown

**Decision**: `Ecommerce.Shared.Exceptions.ForbiddenException(message)`, mapped by `GlobalExceptionHandler`
to 403 "Forbidden" and added to the exceptions whose message is shown in every environment. It is for "the
caller is known and the answer is no": a stopped account after the right password, a moderator reaching past
what moderators may do. "Not yours" stays a 404.

**Rationale**: before this there was no way for a handler to answer 403 with a sentence - an attribute's
403 has no body worth reading, and `UnauthorizedAccessException` is 401. The sentence matters here: the
locked person needs the reason and date, and a moderator needs to know the cap is 30 days (Bruno asserts
the detail contains "30 days"). The 404 rule from specs/027 stands because a 403 confirms the thing exists.
One shared type, like `NotFoundException` and `ConflictException`, because a per-service copy would fall
through the shared handler to a 500.

**Alternatives considered**:

- **Throw `UnauthorizedAccessException`.** Rejected: that is 401, which the storefront reads as "sign in
  again" or "wrong password".
- **Return `Forbid()` from the controller.** Rejected: the decision is in the handler, and the controller
  does nothing but dispatch.

---

## D9 - Self and administrator targets are 409; a moderator on a moderator is 403

**Decision**: `EnsureMayStop` throws `ConflictException` (409) for "your own account" and for "an
administrator", and `ForbiddenException` (403) for a moderator acting on a moderator.

**Rationale**: the record gives no reason beyond the code; the distinction the code draws is this. Nobody
may stop themselves or an administrator - the request conflicts with the target's state whoever sends it,
so no role would make it succeed. A moderator acting on a moderator is refused because of the **caller's**
role - an administrator sending the same request succeeds - which is what 403 means.

**Alternatives considered**:

- **403 for all three.** Not recorded as considered.

---

## D10 - The console draws per role; the server decides

**Decision**: `/admin` requires Admin **or** Moderator (`RequireRole` accepts several roles). `AdminLayout`
marks Fulfilment, Payouts and Audit `adminOnly` and filters them out for a moderator; `AdminHome` shows an
administrator the fulfilment queue and redirects a moderator to `/admin/users`. The users page offers each
row only the actions the caller's role allows, disables Lock and Ban where `EnsureMayStop` would refuse, and
the stop dialog offers a moderator no length above 30 days. Every refusal from the server is shown in its
own words.

**Rationale**: a link to a page that answers 403 is a link to an error (the layout's comment). The page's
own comment: "That is drawing. The server refuses each of those on its own." The CLAUDE.md rule - a role on
the response is for drawing, never for deciding - applies unchanged.

**Alternatives considered**:

- **A separate moderator console.** Not recorded as considered; one console with a filtered sidebar kept
  one layout for both.

---

## D11 - Search is a substring of the email or the name, newest first

**Decision**: `SearchAsync` lower-cases the search and matches it with `Contains` against the lower-cased
email and against `FirstName + " " + LastName`, ordered by `CreatedAt` descending then `Id`, paged with the
shop's page size (default 12, at most 50). No index was added.

**Rationale**: staff look people up by whatever part of an address or a name they have; the issue asked for
"by email". Lower-casing both sides makes the match case-insensitive, consistent with emails being compared
case-insensitively (#49). No measurement was recorded; at the number of accounts the shop has, a scan was
accepted.

**Alternatives considered**:

- **Exact email only.** Rejected by the page's purpose: staff often have only part of it.
- **A trigram index.** Not recorded as considered for this table. Catalog's product search got one later
  (specs/074).

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| Access tokens outlive a stop | A stopped person keeps using the API for up to 15 minutes | Accepted in the spec; specs/065 (#112) later refused them within seconds |
| Unlock has no target rules | A moderator can undo an administrator's lock, free another moderator, or free themselves with a live access token | Found as #121; specs/050 added `EnsureMayRelease` |
| Re-locking replaces the end date | A moderator could shorten an administrator's longer lock by locking again for fewer days | Not recorded as considered; the page does not offer Lock on a locked row |
| Search scans `users` | Slower as accounts grow | Not measured; revisit if the page slows |
