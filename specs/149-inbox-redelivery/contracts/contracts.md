# Contracts: A message delivered twice at once is consumed once and faults neither time

No HTTP, message or gRPC change. The retry policy every service calls gains one case:

```csharp
// Ecommerce.Shared/Messaging/TransientRetry.cs
public static bool IsTransient(Exception exception)
//   40001 serialization failure              -> true   (specs/145)
//   40P01 deadlock                           -> true   (specs/145)
//   Npgsql exception with IsTransient        -> true   (specs/145)
//   23505 on AK_InboxState_MessageId_ConsumerId -> true   (specs/149)
//   23505 on any other constraint            -> false
//   anything else                            -> false
```
