# Contracts: Checkouts of one product do not queue behind retries

No HTTP, message or gRPC change. Configuration only:

```csharp
x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
{
    o.UsePostgres();
    o.IsolationLevel = IsolationLevel.ReadCommitted;   // specs/146
    o.UseBusOutbox();
});
```
