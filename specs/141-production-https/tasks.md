---
description: "Task list for A production stack served over HTTPS"
---

# Tasks: A production stack served over HTTPS

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Services (US2, US3)

- [ ] T001 Gateway `GATEWAY_FORWARD_LIMIT`, validated; tests for one and two hops and a forged header
- [ ] T002 Identity SMTP: credentials, STARTTLS, sender; startup validation; tests

## Phase 2: The stack (US1)

- [ ] T003 `docker-compose.prod.yml`: published images by `RELEASE`, no ports, no build, Caddy, tools behind profiles
- [ ] T004 `deploy/Caddyfile`, `deploy/production.env.example`
- [ ] T005 Started locally over HTTPS; Playwright against `https://shop.localhost`

## Phase 3: Docs

- [ ] T006 `docs/infrastructure/production.md`, running-in-containers, CLAUDE.md, timeline, backlog
- [ ] T007 Merged, closes #287

## Evidence

(Filled in when the work is verified.)
