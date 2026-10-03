---
description: "Task list for A production stack served over HTTPS"
---

# Tasks: A production stack served over HTTPS

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Services (US2, US3)

- [x] T001 Gateway `GATEWAY_FORWARD_LIMIT`, validated; tests for one and two hops and a forged header
- [x] T002 Identity SMTP: credentials, STARTTLS, sender; startup validation; tests

## Phase 2: The stack (US1)

- [x] T003 `docker-compose.prod.yml`: published images by `RELEASE`, no ports, no build, Caddy, tools behind profiles
- [x] T004 `deploy/Caddyfile`, `deploy/production.env.example`
- [x] T005 Started locally over HTTPS; Playwright against `https://shop.localhost`

## Phase 3: Docs

- [x] T006 `docs/infrastructure/production.md`, running-in-containers, CLAUDE.md, timeline, backlog
- [x] T007 Merged, closes #287 - #296

## Evidence

- `Ecommerce.ApiGateway.Tests`: 19/19 pass. `Two_trusted_hops_reach_the_visitor`, `Two_hops_believe_nothing_behind_an_untrusted_one`, and `0`, `6` and `two` refused at startup.
- `Ecommerce.Identity.Tests`, filtered to SMTP and email: 70/70 pass. `SmtpSettingsTests` 5/5.
- Mutations, each caught by a failing test:
  - `ForwardLimit` fixed at 1 → a two-hops test fails;
  - the 1-5 range check removed → 2 failures;
  - `EnableSsl = false` → 1 failure;
  - credentials never set → 1 failure;
  - the half-account check removed → 2 failures.
- The production overlay run locally (images tagged `local`, `shop.localhost` / `portal.shop.localhost`, Caddy's own CA):
  - only `ecommerce-caddy` publishes ports (80, 443, 443/udp);
  - both apps answer 200 over HTTPS, and `/app-config.js` carries the `https://` addresses;
  - `/api/identity/health` answers through Caddy → nginx → gateway;
  - `http://` gives a 308 to `https://`.
- Playwright passes 8/8 over HTTPS with `E2E_IGNORE_HTTPS_ERRORS=1`: back-office sign-in with a code, the storefront → back-office handoff, a customer refused, and the storefront flows.
- Found while verifying: Docker had given the gateway `172.30.10.2`, so Caddy failed with "Address already in use". Fixed with `ip_range: 172.30.10.128/25` on `edge`.
