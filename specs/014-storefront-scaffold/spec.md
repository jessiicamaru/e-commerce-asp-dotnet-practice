# Feature Specification: A Storefront That Builds and Reaches the Gateway

> Completed on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull
> request and docs/architecture/storefront.md (the storefront has no page of its own under
> docs/features/).

**Feature Branch**: `014-storefront-scaffold` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#42](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/42), 2026-09-22 (02:34, UTC+7)

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

(The decisions were written out on issue #34 "so every sub-issue inherits them"; they also say that
checkout is presented by polling, and that anything the interface needs but the backend lacks is filed
as its own issue rather than worked around in the client.)

## User Scenarios & Testing

### User Story 1 - A developer starts the storefront and it reaches the backend (Priority: P1)

A developer with the backend running starts the storefront and sees, on one page, that every service
answers through the gateway.

**Why this priority**: it is the whole feature. Every later page (#35-#39) needs a project that builds
and a way to call the API that already works.

**Independent Test**: with the containers up, `npm run dev` in `client/` and open
`http://localhost:5173/status`.

**Acceptance Scenarios**:

1. **Given** the backend running behind the gateway on `:5000`, **When** the status page loads,
   **Then** identity, catalog, order, inventory, payment and cart each read "up".
2. **Given** one service stopped, **When** the status page loads, **Then** that service reads
   "down" with the status or error, and the others still read "up".
3. **Given** a gateway somewhere other than `:5000`, **When** the developer sets `GATEWAY_URL`,
   **Then** `/api` is forwarded there.

### User Story 2 - A pull request that breaks the storefront is caught (Priority: P1)

Any change - to the client or to anything it depends on - is linted, type-checked and built in CI.

**Why this priority**: equal first. Without it the client can stop compiling on `main` and nobody
notices until the next person opens it.

**Independent Test**: open a pull request; the `Storefront build` job runs `npm ci`, lint and build.

**Acceptance Scenarios**:

1. **Given** a pull request, **When** CI runs, **Then** the `client` job installs, lints (oxlint),
   type-checks and builds `client/`, alongside the backend jobs.
2. **Given** a type error in `client/src`, **When** CI runs, **Then** the job fails.

### User Story 3 - Every later page calls the API one way (Priority: P2)

A page calls the backend through one function that turns an error into something it can show, carries
the access token, and quietly renews an expired one.

**Why this priority**: second, because nothing signs in yet (#35). But written now, so no page invents
its own fetch.

**Independent Test**: call `api()` against an endpoint that answers 400 with ProblemDetails, and read
`ApiError.fieldErrors`.

**Acceptance Scenarios**:

1. **Given** a 400 ProblemDetails with an `errors` object, **When** `api()` receives it, **Then** it
   throws an `ApiError` whose `fieldErrors` maps each field to its first message.
2. **Given** a 401 on a request that is not anonymous, **When** a refresh succeeds, **Then** the
   request is retried once, and only once.
3. **Given** a 204, **When** `api()` receives it, **Then** it resolves with no body instead of failing
   to parse.

### Edge Cases

- **The Orchestrator has no health endpoint** (it had no HTTP surface at the time), so it cannot be on
  the status page; the page says so.
- **An error body that is not JSON** (a proxy error page): `api()` keeps the status and throws an
  `ApiError` without a title.
- **An anonymous call** (sign-in, sign-up, refresh) must never trigger the refresh-and-retry, or a
  failed sign-in would loop.

## Requirements

- **FR-001**: `client/` builds with one command, and CI type-checks, lints and builds it on every PR.
- **FR-002**: In development `/api/*` reaches the gateway; the browser sees one origin.
- **FR-003**: One HTTP layer turns every ProblemDetails error into a typed error with per-field messages,
  attaches the token, and retries once after a silent refresh on 401.
- **FR-004**: A page proves the connection: every service's health through the gateway.
- **FR-005**: CLAUDE.md stops saying there is no frontend.
- **FR-006**: The gateway the proxy targets is configurable (`GATEWAY_URL`), defaulting to
  `http://localhost:5000`.
- **FR-007**: Pages call the API through the one HTTP layer. The status page is the one exception at the
  merge: it calls `fetch` on `/api/<service>/health` directly, although `client/README.md` describes
  `http.ts` as "the only way to call the backend".

### Key Entities

- **ApiError** - an HTTP status, the ProblemDetails it came with, and its field errors flattened to one
  message per field.
- **Service health** - one of `checking`, `up`, or `down` with the status code or error.

## Success Criteria

- **SC-001**: `npm run build` and `npm run lint` pass in CI.
- **SC-002**: With the backend running, all six health checks read "up" through the dev proxy.
- **SC-003**: A client change that does not type-check fails the pull request's checks.

## Assumptions

- The storefront runs only in development for now, behind Vite's proxy; there is nowhere to deploy it
  (a production image came later, specs/051).
- A cross-origin deployment would need CORS at the gateway, and only there (#34); not needed yet.
- One currency, English words (translation came with specs/021).

## Out of Scope

- Any shopping page: sign-in (#35), the catalogue (#36), cart and addresses (#37), checkout and order
  history (#38, #39).
- Styling beyond a readable default; polish is explicitly not the goal (#23).
- Unit tests for the client (none existed until specs/028).
