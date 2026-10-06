# Research: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

## D1. It is MassTransit 8.5.11, not specs/154

From issue #353 and its comment (2026-10-06):

| Runs of `fault.sh broker` | MassTransit | Reconnect policy | Endpoint lost for good |
| :-- | :-- | :-- | :-- |
| 7 (specs/154's A/B) | 8.3.6 | quick and default | 0 of 7 |
| 2 | 8.5.11 | quick (specs/154) | 2 of 2: Inventory `ReserveInventory` |
| 1 | 8.5.11 | default (`Messaging__ReconnectQuickly=false`) | 1 of 1: Order `OrderSvcOrderCompleted`, `OrderSvcOrderFailed`, `OrderSvcEraseAccountFromOrders` |

The run with MassTransit's own schedule failed too, so the reconnect policy is not what stops the endpoint. The common
factor is the version.

**Lead, not established**: at the outage, consumes were mid-flight on a `FOR UPDATE` and were cancelled
(`57014: canceling statement due to user request`). Diffing 8.3.6 and 8.5.11, `ReceiveTransport<T>.ReceiveTransportAgent.Run`
no longer rethrows an `OperationCanceledException`, which would let a cancelled consume end the agent's loop instead of
retrying. The logs that would show which branch exited were lost with the container.

## D2. Pin 8.3.6 rather than handle the fault

**Decision** (with the user): every `MassTransit*` reference back to 8.3.6.

**Rationale**:
- 8.3.6 is what specs/145, 149 and 154 were built and measured on, and it passed seven broker outages.
- Nothing in the code needed 8.5: #326 changed versions only.
- It is reversible by one Dependabot rule and one test once the cause is known.

**Alternatives rejected**:
- *Detect a stopped endpoint and restart it* (health check unhealthy, or a watchdog): code against an internal failure
  we have not characterised, needing its own fault runs, and it still leaves a stall until it triggers.
- *Stay on 8.5.11 and report it upstream first*: checkout stops on the next broker restart in the meantime.
- *8.4.x*: untested either way; the point is a version with evidence.

## D3. Keep it pinned

- **Dependabot**: `ignore` every MassTransit update. First this was minors and majors only, leaving 8.3.x patches
  proposed for security fixes; the 8.3.7 patch arrived within the hour (#357). A patch is no more testable by CI than a
  minor, and the regression's version is unknown, so it is ignored too. A security fix is not missed: the build fails
  on a vulnerable package (specs/150).
- **A test**, because the ignore does not stop a person and CI cannot see the defect: `MassTransitVersionTests` reads
  every `.csproj` under the solution and asserts each MassTransit reference is exactly 8.3.6, and asserts the loaded
  `MassTransit.Abstractions` is 8.3.6. The message names #353 and the way to move: `fault.sh broker` several times.

## D4. How it is judged

`fault.sh broker` three or more times on the stack rebuilt from this branch, reading after each run that no queue holds
messages with zero consumers (`rabbitmqctl list_queues name messages consumers`). Results in [tasks.md](tasks.md).
