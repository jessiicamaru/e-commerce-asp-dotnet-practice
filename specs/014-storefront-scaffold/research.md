# Research: A Storefront That Builds and Reaches the Gateway

> Written on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull request
> and docs/architecture/storefront.md (the storefront has no page of its own under docs/features/).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

Decisions D1-D4 are the storefront-wide ones taken on the owner's instruction to choose the recommended
option and written out on issue #34; issue #23 had listed them as questions, with the alternatives. D5
and D6 are how the scaffold carried them out.

## D1 - The client lives in this repository, under `client/`

**Decision**: a top-level `client/` beside `server/`.

**Rationale** (#34): "A solo practice repo; one PR can change an endpoint and its only consumer
together, and CI checks both."

**Alternatives considered**:

- **A separate repository** (#23): it "versions and deploys independently and immediately makes the
  contract between them a real contract rather than a convention". Rejected for now: nothing is
  deployed, and the value of this client is finding what the API lacks, which is faster when the fix
  and the page land in one pull request.

## D2 - React + Vite + TypeScript, deliberately thin

**Decision**: React 19, Vite 8, TypeScript 6, `react-router-dom` for pages, oxlint for linting.

**Rationale**: #23 asked "how much of this is worth doing at all, given the stated goal is learning
backend architecture", and answered its own question: "a deliberately thin interface that exercises
every endpoint may be worth more than a polished one that exercises a third." #34: "The goal is
exercising the API, not polish."

**Alternatives considered**: a polished storefront. Rejected by #23's reasoning above. Why React over
another framework, and oxlint over ESLint, is not recorded.

## D3 - Talk only to the gateway, through Vite's proxy

**Decision**: every call goes to `/api/...` on the page's own origin; Vite forwards `/api` to
`GATEWAY_URL` (default `http://localhost:5000`) with `changeOrigin: false`.

**Rationale**: one origin means **no CORS** - which #23 noted "does not exist yet in any service, and is
the first thing a browser will hit" - and it means Identity's HttpOnly refresh cookie is a same-origin
cookie, exactly as it would be behind a real reverse proxy (`vite.config.ts`).

**Alternatives considered**:

- **Calling each service directly.** Rejected: it bypasses the gateway, which is the system's one public
  entry, and needs CORS in every service.
- **Calling the gateway cross-origin.** Rejected for development; #34 records that "a real cross-origin
  deployment needs CORS at the gateway, and only there".

## D4 - The access token in memory, the refresh token in the existing HttpOnly cookie

**Decision**: the hybrid flow `docs/features/auth/security-best-practices.md` argues for. The call
layer reads the token through a provider function (`configureAuth`) and never stores it.

**Rationale**: "anything a script can read, an injected script can steal" (the comment #35 put on
`AuthContext`); the refresh cookie is HttpOnly, so no script reads it at all. #23: "The token story
exists and has never met a browser."

**Alternatives considered**: `localStorage` or `sessionStorage`. Rejected for the reason above.

## D5 - One call layer, `src/api/http.ts`

**Decision**: `api<T>(path, options)` prefixes `/api`, sends JSON, adds `Authorization: Bearer` unless
the call is `anonymous`, sends credentials (`credentials: 'include'`) so the refresh cookie travels,
and on a 401 asks the auth layer to refresh once and retries once. A non-2xx response becomes an
`ApiError` carrying the status and the ProblemDetails, with `fieldErrors` flattening
`errors: { Field: [msg, ...] }` to `{ Field: msg }`. A 204 resolves with no body.

**Rationale**: every service answers errors as RFC 7807 ProblemDetails through the shared
`GlobalExceptionHandler`, so one shape can be turned into one error type; "the page decides how to word
them for a person" (the file's own comment). Retrying only once, and never for anonymous calls, stops a
failed sign-in or refresh from looping.

**Alternatives considered**: a fetch per page. Rejected: seven pages would each re-implement token
handling and error parsing. (Today axios and TanStack Query stand where this layer stood - see CLAUDE.md
and `client/README.md`.)

## D6 - A status page as the first page, and CI from day one

**Decision**: `/status` fetches `/api/<service>/health` for identity, catalog, order, inventory,
payment and cart, and shows each as up or down. CI gains a `client` job - Node 22, `npm ci`,
`npm run lint`, `npm run build` - independent of the backend jobs so it runs beside them.

**Rationale**: the page is "still the quickest proof that /api reaches the gateway and the gateway
reaches everything" (its comment). The CI job is what makes FR-001 true for every later pull request;
it "publishes nothing yet - there is nowhere to deploy it - so the proof is that it builds against the
API it was written for" (its comment in `ci.yml`).

**Alternatives considered**: not recorded.
