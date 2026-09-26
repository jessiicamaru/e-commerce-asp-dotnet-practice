# Message Contracts: Email

> Written on 2026-09-27, after the feature merged (#143), from the code at that merge, the pull request and
> [docs/features/email.md](../../../docs/features/email.md).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new message. No HTTP endpoint and no gRPC service was added or changed.

---

## `EmailRequested` - `Ecommerce.Contracts.Identity` (new)

```csharp
record EmailRequested(
    Guid EmailId,
    Guid RecipientId,
    string Template,
    Dictionary<string, string> Data,
    string Language,
    DateTime RequestedAt);
```

It is a request **to** Identity, so it lives with Identity's contracts.

| Field | Meaning |
| :--- | :--- |
| `EmailId` | Minted by the sender (`Guid.CreateVersion7()`); Identity keeps each request once by it |
| `RecipientId` | The person. Never an address: Identity reads that from its own `users` |
| `Template` | A constant on `EmailTemplate` in `Ecommerce.Shared.Email`; at the merge only `OrderPaid` |
| `Data` | The values the template needs. `OrderPaid`: `orderId`, `total`, `currency` |
| `Language` | What the email is written in - for an order, the language it was placed in |
| `RequestedAt` | When it was asked for |

**Publisher**: any service, through `IEmailSender.SendAsync(recipientId, template, data, language)` registered by
`AddEmailSender()`. It publishes through the caller's outbox, so the caller sends before its one save or inside a
repository's `stage`. At the merge the one publisher is **Order**, from `OrderNotices.PaidAsync` inside
`CompleteOrderCommandHandler`'s settle stage.

**Consumer**: **Identity**, `QueueEmailConsumer` (Identity's first consumer; the class name is the queue name, so
it is named for what it does). It sends `QueueEmailCommand`, which inserts into `outgoing_emails` with
`ON CONFLICT ("Id") DO NOTHING`. It never sends: a mail server that is down must not fill the error queue.

**Idempotency**:

| Duplicate | Handled by |
| :--- | :--- |
| The settlement is redelivered | Order's guarded settle affects zero rows; no second `EmailRequested` is staged |
| `EmailRequested` is redelivered | `ON CONFLICT ("Id") DO NOTHING`; no second row, no second email |
| Two Identity instances sweep at once | `FOR UPDATE SKIP LOCKED` on the claim |
| A send succeeds and its commit fails | Not prevented: that one email goes out twice (at-least-once, research D3) |

**Deployment**: a new contract - Order and Identity must be deployed together (PR #143).

---

## Outside the broker: SMTP

Identity's `SmtpEmailTransport` hands a plain-text UTF-8 message to `Email:SmtpHost`:`Email:SmtpPort`
(`SMTP_HOST` / `SMTP_PORT`; Mailpit on 1025 by default) from `Email:From`
(`e-commerce <no-reply@ecommerce.local>`), with no authentication and no TLS.
