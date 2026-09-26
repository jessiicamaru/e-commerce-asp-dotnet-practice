# Message Contracts: Moderators, locks and bans

> Written on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No message contract was added or changed.** Identity publishes two existing messages from
`Ecommerce.Contracts/Activity` with new values, through its transactional outbox, in the same save as the
change they describe. Identity consumes nothing new.

---

## Published: `AuditEntryRecorded` (specs/041)

```csharp
record AuditEntryRecorded(Guid EntryId, string Category, string Action, Guid? ActorId, string? ActorEmail,
    string? ActorRole, string SubjectType, string? SubjectId, string Summary, string? Before, string? After,
    string Service, DateTime OccurredAt);
```

**Publisher**: Identity, through `IAuditTrail.RecordAsync`, called before the one `SaveChangesAsync`.
**Consumer**: Activity, which keeps it with `INSERT ... ON CONFLICT DO NOTHING` on `EntryId` - a redelivery
is recorded once.

| Action | Category | Subject | Written when | Before / After |
| :-- | :-- | :-- | :-- | :-- |
| `RoleGranted` | Security | `User` / the person's id | a grant that changed something | snapshot |
| `RoleRevoked` | Security | `User` | a revoke that changed something | snapshot |
| `AccountLocked` | Moderation | `User` | every lock | snapshot |
| `AccountUnlocked` | Moderation | `User` | an unlock that cleared a lock | snapshot |
| `AccountBanned` | Moderation | `User` | every ban | snapshot |
| `BanLifted` | Moderation | `User` | a lift that cleared a ban | snapshot |
| `SignInRefused` | Security | `User` | the right password on a stopped account (new case; a wrong password already recorded one since specs/041) | none |

The snapshot is `{ roles, lockedUntil, lockReason, bannedAt, banReason }` - roles sorted, nothing personal.
The actor is the caller from the token, labelled with the most powerful role held (so a moderator's lock is
`ActorRole = "Moderator"`); for `SignInRefused` the actor is the account itself. `Moderation` was already one
of the seven categories and `Moderator` already one of the actor roles, both from specs/041.

## Published: `UserNotificationRequested` (specs/042)

```csharp
record UserNotificationRequested(Guid NotificationId, Guid RecipientId, string Kind,
    Dictionary<string, string> Data, string? Link, DateTime OccurredAt);
```

**Publisher**: Identity, through `INotifier.NotifyAsync` (registered in Identity for the first time by this
feature), before the one save. **Consumer**: Activity, idempotent on `NotificationId`.

| Kind | Recipient | Data | Link | Storefront wording (en) |
| :-- | :-- | :-- | :-- | :-- |
| `ModeratorGranted` | the person granted | none | `/admin` | "You are now a moderator. The console is in your menu." |
| `ModeratorRevoked` | the person revoked | none | none | "You are no longer a moderator." |

The two kinds are constants in `Ecommerce.Shared/Notifications/Notifier.cs` (`NotificationKind`), worded in
`client/src/locales/{en,vi}/notifications.json`. A lock or a ban notified nobody at this merge.

---

## Delivery guarantees

| Property | Where it is handled |
| :-- | :-- |
| The change saved but the message lost, or the reverse | The outbox: the entry and the change commit in one `SaveChangesAsync` |
| The same entry or notice delivered twice | Activity's unique key on the publisher's id |
| The same request sent twice | Grant, revoke, unlock and lift publish nothing when there is nothing to change; a repeated lock or ban is a new action and is recorded again |

## Not added

No `UserLocked` or `RoleChanged` integration event: no other service needed to know at this merge, since
every service reads roles from the token. Specs/065 later added `AccessTokensRevoked` so that a stop reaches
live access tokens in every service.
