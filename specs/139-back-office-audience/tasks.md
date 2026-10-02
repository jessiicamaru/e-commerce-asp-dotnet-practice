---
description: "Task list for A separate token audience for the back office"
---

# Tasks: A separate token audience for the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared and Identity (US1, US2)

- [x] T001 `JwtSettings.BackOfficeAudience`; both audiences valid; staff roles removed outside the back office
- [x] T002 Identity issues back-office sessions' tokens for `BackOfficeAudience`
- [x] T003 `BackOfficeAudienceTests` through a real pipeline; Identity's issued audience

## Phase 2: Verification and docs

- [x] T004 Mutations, each red; Bruno; the verify scripts; Playwright
- [x] T005 Docs: auth pages, back office, ADR-003 progress, CLAUDE.md, timeline, backlog
- [x] T006 Merged, closes #280 - #285

## Evidence

- **`BackOfficeAudienceTests`**, through a real ASP.NET Core pipeline (TestServer) with hand-signed tokens:
  - a back-office token with `Admin` gets 200 on a staff endpoint;
  - a storefront token with `Admin`, `Customer` and `Moderator` gets 403 on the staff endpoint, 200 on a signed-in one, and its roles read `Customer`;
  - a token for neither audience gets 401;
  - a configured audience is the one honoured;
  - an empty or blank setting is the default.
- **Identity**: `BackOfficeSessionTests` asserts each app's `aud` (`EcommerceClients` for the storefront, `EcommerceBackOffice` for the back office).
- **A defect found by a test**: with the setting present but null (the binder's reading of an empty value), every back-office token was 401 in every service. The setting now treats empty as the default.
- **Mutations**, each red:
  - A1: roles never removed.
  - A2: every role removed.
  - A3: one audience accepted.
  - A4: one audience issued.
  - A5: an empty audience kept. This one first survived, because the test only covered null; the `""` and `"   "` cases were added.
- **All nine server suites**: Orchestrator 18, ApiGateway 14, Payment 31, Cart 22, Catalog 268, Inventory 78, Order 366, Identity 278, Activity 52.
- **Every service rebuilt** (each validates tokens): `verify-auth.sh` and `verify-saga.sh` pass, Bruno 403/403, Playwright 7/7.
- **Docs**: TOTP page §7, the back office page, ADR-003 progress and alternatives, CLAUDE.md.
