# Contracts: A consumer survives a transient database failure

No HTTP, message or gRPC shape changes. The one new interface is a building block:

```csharp
// Ecommerce.Shared.Messaging
public static class TransientRetry
{
    public static bool IsTransient(Exception exception);                 // 40001, 40P01, or a transient connection
    public static void UseTransientRetry(this IConsumePipeConfigurator configurator);
}
```

Every service calls it in its endpoint callback, **before** the EF outbox:

```csharp
x.AddConfigureEndpointsCallback((context, _, cfg) =>
{
    cfg.UseTransientRetry();
    cfg.UseEntityFrameworkOutbox<TDbContext>(context);
});
```
