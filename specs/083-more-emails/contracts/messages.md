# Message Contract: Emails for what happens to people

> Written on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature**: [spec.md](../spec.md)

No message record changed. `EmailRequested` (specs/060) gained new publishers, new `Template` values and a meaning
for an empty `Language`.

---

## `EmailRequested` - `Ecommerce.Contracts.Identity`

```csharp
record EmailRequested(Guid EmailId, Guid RecipientId, string Template,
                      Dictionary<string, string> Data, string Language, DateTime RequestedAt);
```

**Publishers** (through each service's transactional outbox, via `IEmailSender.SendAsync`):

| Publisher | Templates added here | `Language` sent |
| :--- | :--- | :--- |
| Order | `ParcelShipped`, `OrderCancelled`, `ReturnAccepted`, `ReturnRefused`, `ReturnRefunded` | `orders.Language` (the order's) |
| Catalog (new publisher; `AddEmailSender` added) | `SavedBackInStock` | `""` (`EmailTemplate.ReadersLanguage`) |
| Identity (new publisher of its own requests; `AddEmailSender` added) | `AccountLocked`, `AccountBanned` | `""` |

Identity publishing to itself is deliberate: the request rides the same outbox as the lock or ban, and the email
row is written by the same consumer as everybody else's.

**Consumer**: Identity, `QueueEmailConsumer` → `QueueEmailCommand`.

**New behaviour**: a blank `Language` is resolved from `users.Language` of `RecipientId`, then
`EmailTemplates.DefaultLanguage` (`vi`). A non-blank one is used as sent, lower-cased.

**Idempotency**: unchanged - `outgoing_emails` is keyed on `EmailId`, so a redelivered request queues nothing
twice (specs/060).

**Ordering and atomicity**: each request is staged before the sender's one `SaveChangesAsync`, or inside the
`stage` callback of a guarded move, so it exists only if the change it describes committed.

## `EmailTemplate` constants - `Ecommerce.Shared.Email`

Added: `ParcelShipped`, `OrderCancelled`, `ReturnAccepted`, `ReturnRefused`, `ReturnRefunded`, `SavedBackInStock`,
`AccountLocked`, `AccountBanned`, and `ReadersLanguage = ""`. A service rebuilt without these still compiles
against older constants and simply never asks for the new emails.
