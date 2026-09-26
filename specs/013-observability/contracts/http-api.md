# HTTP Contract: Following One Order Across Seven Services

> Written on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull request
> and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](../spec.md) | **Decision**: [research.md D3](../research.md)

**No endpoint was added, removed or changed**, in any service or the gateway. Two HTTP behaviours
changed: how the gateway treats trace context on the way in, and a new outbound call from every
service to the telemetry collector.

---

## Inbound at the gateway (`:5000`) - trace context is ignored

| Header a client sends | Before | After |
| :--- | :--- | :--- |
| `traceparent`, `tracestate` | Could be adopted as the request's parent | **Ignored.** Every request starts a new trace at the gateway |
| `baggage` | Could be extracted | **Ignored** (`ExtractBaggage` returns nothing) |

Registered as `DistributedContextPropagator` in the gateway's DI container
(`IgnoreIncomingTraceContextPropagator`). Outbound from the gateway to the services, YARP injects the
gateway's own `traceparent` through the default propagator, so the services continue the gateway's
trace. Services other than the gateway use the default propagator both ways.

Nothing is added to any response: no trace id header is returned to the caller.

## Inbound at every service - `/health` is not traced

`GET /health` is still served exactly as before; it is excluded from the ASP.NET Core instrumentation
so the probes Docker sends every few seconds do not bury the checkouts.

## Outbound from every service - OTLP to the collector

Only when `OTLP_ENDPOINT` is set:

| Call | Target | Body |
| :--- | :--- | :--- |
| `POST ${OTLP_ENDPOINT}/v1/logs` | Seq, `http://localhost:5341/ingest/otlp` on the host, `http://seq:5341/ingest/otlp` in containers | OTLP logs, protobuf |
| `POST ${OTLP_ENDPOINT}/v1/traces` | the same | OTLP traces, protobuf |

`X-Seq-ApiKey: ${OTLP_API_KEY}` is added when that variable is set. When `OTLP_ENDPOINT` is unset no
exporter is registered at all, so a missing or unreachable collector cannot fail a request.

## Seq's own ports (compose)

`5341` - ingestion (OTLP at `/ingest/otlp`); `5380` - the UI (container port 80), user `admin`.
