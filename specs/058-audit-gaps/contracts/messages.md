# Message Contracts: Audit gaps

> Written on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

No message type was added or changed. The feature publishes an existing contract from five more places.

---

## Published: `AuditEntryRecorded` - `Ecommerce.Contracts.Activity`

```csharp
record AuditEntryRecorded(
    Guid EntryId, string Category, string Action,
    Guid? ActorId, string? ActorEmail, string? ActorRole,
    string SubjectType, string? SubjectId, string Summary,
    string? Before, string? After, string Service, DateTime OccurredAt);
```

Published through `IAuditTrail.RecordAsync` (`Ecommerce.Shared.Audit`), which stages it in the calling service's
transactional outbox. Snapshots are serialised and redacted (any property named like password, token, secret or
hash) before they leave the service.

| Action | Publisher | Handler | Category | Actor |
| :--- | :--- | :--- | :--- | :--- |
| `CategoryTranslated` | Catalog | `SetCategoryTranslationCommandHandler` | Catalog | the caller (Admin) |
| `CategoryTranslationRemoved` | Catalog | `RemoveCategoryTranslationCommandHandler` | Catalog | the caller (Admin) |
| `ProductTranslationRemoved` | Catalog | `RemoveProductTranslationCommandHandler` | Catalog | the caller (Seller or Admin) |
| `DefaultAddressChanged` | Identity | `SetDefaultAddressCommandHandler` | User | the caller |
| `SignedOut` | Identity | `LogoutCommandHandler` | Security | the person, from the session row (`AuditActors.Of(user)`) |
| `SessionReuseDetected` | Identity | `RefreshTokenCommandHandler` | Security | the person, from the session row |

**Consumer**: Activity's `RecordAuditEntryConsumer` (queue name prefixed `ActivitySvc`), unchanged. It stores each
entry once, `INSERT ... ON CONFLICT (Id) DO NOTHING` on `EntryId`, so a redelivery changes nothing.

**What an entry never carries**: address content (`DefaultAddressChanged` has no snapshot), a refresh token
(asserted by `Signing_out_is_recorded_as_the_person_and_nothing_is_recorded_for_nothing`).
