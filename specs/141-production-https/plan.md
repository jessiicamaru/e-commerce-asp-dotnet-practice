# Implementation Plan: A production stack served over HTTPS

**Branch**: `feat/287-production-https` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #287

## Summary

A third compose overlay runs the published images behind Caddy, with automatic HTTPS for two domains and no other
published port. The gateway learns to read two forwarded hops when told to. Identity learns authenticated, STARTTLS
SMTP. Everything is verified on this machine over real HTTPS.

## Technical Context

**Language/Version**: C# / .NET 10; Docker Compose v2.24+ (`!reset`); Caddy 2
**Primary Dependencies**: ASP.NET Core forwarded headers; `System.Net.Mail`
**Storage**: the existing volumes; Caddy's own (`caddy_data`) for certificates
**Testing**: gateway tests (WebApplicationFactory), Identity tests, Playwright against `https://shop.localhost`
**Target Platform**: one Linux server with Docker
**Project Type**: deployment configuration + two small service changes
**Performance Goals**: n/a
**Constraints**: development unchanged; production names images by `sha-` only
**Scale/Scope**: one overlay, one Caddyfile, one env template, two settings

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service keeps its own database and configuration; the overlay only changes where images come from and what is exposed. |
| **II. Clean Architecture Layering** | **Pass.** The SMTP options live in Infrastructure; the forwarding limit lives in the gateway's setup. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No change to writes or messages. |
| **IV. Identity Comes From the Token** | **Pass.** Unchanged. The forwarded address keys rate limits only, never identity. |
| **V. Evidence Over Assumption** | **Planned.** Gateway tests for the hop count; Identity tests for SMTP settings; the whole stack started from the overlay and driven by Playwright over HTTPS. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/141-production-https/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/docker-compose.prod.yml
server/deploy/{Caddyfile,production.env.example}
server/src/ApiGateway/Ecommerce.ApiGateway/AuthRateLimits.cs          GATEWAY_FORWARD_LIMIT
server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Email   SMTP auth + STARTTLS
server/tests/Ecommerce.ApiGateway.Tests, server/tests/Ecommerce.Identity.Tests
docs/infrastructure/production.md
```

## Complexity Tracking

No violation to justify.
