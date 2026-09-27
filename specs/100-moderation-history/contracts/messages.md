# Message contract: Staff see a person's moderation history

## `Ecommerce.Contracts.Activity.AuditEntryRecorded`: one field added

```csharp
public record AuditEntryRecorded(
    Guid EntryId, string Category, string Action, Guid? ActorId, string? ActorEmail, string? ActorRole,
    string SubjectType, string? SubjectId, string Summary, string? Before, string? After, string Service,
    DateTime OccurredAt,
    Guid? AboutUserId = null);   // new
```

- **Additive.** A publisher from before this feature sends no `AboutUserId`, and Activity reads null. An Activity from
  before this feature ignores the extra property.
- **Filled by** `AuditTrail.RecordAsync(..., aboutUserId:)`. When the argument is omitted and the subject type is `User`,
  the subject id is used.
- **Staged as before**: through each service's outbox, before the one save or inside the `stage` callback of a guarded
  statement. The field changes nothing about when an entry commits.
