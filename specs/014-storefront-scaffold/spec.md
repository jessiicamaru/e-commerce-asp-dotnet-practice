# Feature Specification: A Storefront That Builds and Reaches the Gateway

**Feature Branch**: `014-storefront-scaffold` · **Created**: 2026-09-22 · **Status**: Implemented

**Input**: Issue [#34](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/34), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

No interface has ever consumed this API. Before any page can be built, there has to be a project that
builds, reaches the backend the way a browser would, and is checked by CI — or every later sub-issue
starts by inventing that.

## Decisions already taken

Recorded on #23 on the owner's instruction to choose the recommended option: the client lives in this
repository under `client/`; React + Vite + TypeScript, deliberately thin; it talks only to the gateway,
through Vite's dev proxy (one origin, no CORS); the access token lives in memory and the refresh token
stays in Identity's HttpOnly cookie.

## Requirements

- **FR-001**: `client/` builds with one command, and CI type-checks, lints and builds it on every PR.
- **FR-002**: In development `/api/*` reaches the gateway; the browser sees one origin.
- **FR-003**: One HTTP layer turns every ProblemDetails error into a typed error with per-field messages,
  attaches the token, and retries once after a silent refresh on 401.
- **FR-004**: A page proves the connection: every service's health through the gateway.
- **FR-005**: CLAUDE.md stops saying there is no frontend.

## Success Criteria

- **SC-001**: `npm run build` and `npm run lint` pass in CI.
- **SC-002**: With the backend running, all six health checks read "up" through the dev proxy.
