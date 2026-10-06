# Contracts: Services reconnect to the broker within seconds of its return

No HTTP, message or gRPC change. One shared configuration call:

```csharp
// Ecommerce.Shared/Messaging/BrokerReconnect.cs
public static bool ReconnectQuickly(this IRabbitMqBusFactoryConfigurator configurator)
//   true:  the host's receive-transport retry policy is BrokerReconnect.Policy (1-5 s, the transport's own filters)
//   false: MassTransit's internals differ; its default stays (3-30 s) - BrokerReconnectTests fails in that case
```

Every service: `x.UsingRabbitMq((context, cfg) => { cfg.ReconnectQuickly(); ... })`.
