# Research: Services reconnect to the broker within seconds of its return

## D1. Where the stall comes from: MassTransit's own code

Read from MassTransit 8.3.6, decompiled with ILSpy:

1. **The outbox waits for a healthy bus.** `BusOutboxDeliveryService<TDbContext>.ExecuteAsync` calls
   `WaitForHealthStatus(busControl, BusHealthStatus.Healthy)` before every batch. Nothing leaves an outbox until
   **every** receive endpoint of that service is connected again.
2. **Each receive endpoint reconnects on a fixed policy.** `RabbitMqHostConfiguration`'s constructor sets:

   ```csharp
   ReceiveTransportRetryPolicy = Retry.CreatePolicy(x =>
   {
       x.Handle<ConnectionException>();
       x.Handle<AlreadyClosedException>();
       x.Handle<EndOfStreamException>();
       x.Handle<OperationInterruptedException>(e => e.ChannelShouldBeClosed());
       x.Handle<NotSupportedException>(e => e.Message.Contains("Pipelining of requests forbidden"));
       x.Ignore<AuthenticationFailureException>();
       x.Exponential(1000, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3));
   });
   ```

   The property has a getter only. The transport reads it when each endpoint starts
   (`new ReceiveTransportAgent(hostConfiguration.ReceiveTransportRetryPolicy, ...)`), and sends reuse it
   (`SendTransportRetryPolicy => ReceiveTransportRetryPolicy`).

**Consequence**: after a minute down, every endpoint is retrying every 30 s. An attempt that lands a few seconds before
RabbitMQ is ready costs another 30 s, and the outbox waits for the slowest endpoint. Order's log in the 2026-10-05
run shows exactly this (posted on #304):

```text
15:14:23.8  broker started
15:14:27    Retrying 00:00:30: Broker unreachable    <- 5 s before RabbitMQ was ready (healthy 15:14:32)
15:14:57.7  outbox delivery resumes                  <- the next attempt, 30 s later
```

## D2. Replace the policy, same filters, shorter interval

**Decision**: in `UsingRabbitMq`, replace the host configuration's `ReceiveTransportRetryPolicy` with a policy that
handles exactly the same exceptions and ignores the same one, at `Exponential(1000, 1 s, 5 s, 1 s)`.

**Rationale**:
- **Same filters**: only *when* a connection is retried changes, never *what* is retried.
  `OperationInterruptedExceptionExtensions.ChannelShouldBeClosed` is public, so the filter is the transport's own.
- **5 s cap**: the stall after recovery is bounded by one interval, so 30 s becomes 5 s. While the broker is down, a
  service's 4–6 endpoints try once every 5 s each: about one connection attempt a second per service, which a broker
  that is down cannot even notice, and one that is up handles trivially.
- **1 s jitter**: as before, so instances and endpoints do not retry in lockstep.

**How**: the configurator's `_hostConfiguration` field holds the `RabbitMqHostConfiguration`. Its auto-property's
backing field, `<ReceiveTransportRetryPolicy>k__BackingField`, is set once, during configuration, before the bus
starts.
- If either field is missing (a future MassTransit), the method returns false, the service starts with MassTransit's
  policy, and the host logs that it could not apply.
- `BrokerReconnectTests` asserts it applies, so the same upgrade fails in CI first.

**Alternatives rejected**:
- *Accept and document*: the measured cost is 51–99 s of stalled checkout after every broker restart.
- *A watchdog that restarts the bus once the broker answers*: it stops in-flight consumes, it is much more code, and
  it needs its own tests of the restart.
- *MassTransit 9*: a commercial licence (specs/153).
- *RabbitMQ.Client's automatic recovery*: MassTransit supervises connections itself and does not use it.
- *A shorter `OutboxDeliveryServiceOptions.QueryDelay`*: the outbox is waiting on bus health, not on its sweep.

## D3. How it is judged

Against the four broker-fault runs already recorded with MassTransit's default:

| Run | Backlog cleared after recovery | Orders placed after recovery: settle p50 |
| :-- | --: | --: |
| 2026-10-03 14:05 (#291) | 89.1 s | 61.4 s |
| 2026-10-05 13:52 (#292) | 99.0 s | 78.1 s |
| 2026-10-05 15:08 (#306) | 60.0 s | 40.1 s |
| 2026-10-05 15:12 (#306) | 50.8 s | 29.5 s |

Three runs with the change, on the same machine.

## D4. Corrected: most of the "recovery" was RabbitMQ booting

The first runs with the fix still took 92 s to clear the backlog. The broker's own log, from the 02:53 run (broker
started at 02:56:03):

```text
02:56:13       Docker: e-commerce-rabbitmq healthy          <- rabbitmq-diagnostics ping passes
02:56:21.7     Starting RabbitMQ 3.13.7
02:56:36.6     started TCP listener on [::]:5672            <- only now can anything connect (31 s after start)
02:56:36.8     accepting AMQP connection from ecommerce-order
02:56:41.6     ... activity, identity, catalog, payment, cart, inventory
02:56:42.4     ... orchestrator                             <- every service back 5.8 s after the port opened
```

The services were also logging, every 5 s (the new schedule), `Connection refused` and `Name or service not known`
until 02:56:36. They were retrying correctly against a broker that was not there yet.

**Consequences**:
1. **The metric was wrong.** Counting from `docker start` folds the broker's ~31 s boot into "recovery". Each
   baseline figure (51-99 s) contains it. fault.sh now records `ready_at` (the listener line) and measures from it.
2. **The health check was early.** `ping` checks that the Erlang node answers, not that AMQP listens. Compose's
   `depends_on: condition: service_healthy`, the deploy's `--wait` and fault.sh all trusted it. The check is now
   `check_port_connectivity`, which connects to every listener.
3. **The claim is the reconnect lag**: from the port opening to each service's first connection, read from the
   broker's log by the address it accepted. The comparison is an A/B on one stack, because the earlier baseline runs
   did not record `ready_at`. `Messaging:ReconnectQuickly=false` gives MassTransit's schedule with nothing else
   changed.
4. **What is left is throughput.** After every service is back, the backlog drains at the laptop's pace, about 50 s
   for 264 orders. Making that faster is not this feature.
