# Feature Specification: Storefront image

> Completed on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Feature Branch**: `051-storefront-image` | **Created**: 2026-09-24 | **Issue**: #132

**Status**: Merged (#133, 2026-09-24)

**Input**: Issue #132 - the storefront is built on every change and then thrown away; nothing publishes it and
it cannot be served as built.

## Why

On every merge to `main`, each service and the gateway are built, scanned for secrets and published as an
image, nine in all. The storefront is linted, tested and built, and then its `dist/` is thrown away. It
also could not be served as built: it calls `/api` on its own origin, and only Vite's dev proxy forwards
that to the gateway. So the one way to see the interface is `npm run dev` on a developer's machine.
`docker compose ... up` brings up the whole backend and nothing to click.

The user prioritised this ahead of the remaining P0 defects. It is what makes the whole system,
interface included, runnable from images and demonstrable for the project report.

## User Scenarios & Testing *(mandatory)*

### US1 - The whole system from images (Priority: P1)

Somebody runs `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` and opens
`http://localhost:8088`. The storefront loads. Signing in, browsing, the cart and checkout all work,
because `/api` reaches the gateway on the same origin, exactly as in development.

**Why this priority**: It is the point of the issue - a system that can be shown from images, interface
included.

**Independent Test**: Bring up the full compose stack, open `http://localhost:8088`, sign in, browse, add to
the cart, and reload a deep link.

**Acceptance Scenarios**:

1. `/` serves the app.
2. A deep link such as `/orders/<id>` or `/shop/products/<id>`, opened directly or reloaded, serves the
   app, not a 404.
3. `/api/...` is answered by the gateway, headers and status included. The HttpOnly refresh cookie
   keeps working, because the origin is one.
4. A product photograph up to Catalog's 2 MB limit uploads through it. Nothing in between refuses it
   first.
5. `index.html` is never cached. Hashed assets under `/assets/` are cached for a year and marked
   immutable, so a new release is picked up at the next load and an old one's files are never stale.
6. A missing file under `/assets/` is a 404, never `index.html` (which a browser would try to run as
   JavaScript).

---

### US2 - Published like everything else (Priority: P1)

A merge to `main` publishes `ghcr.io/jessiicamaru/ecommerce-storefront:sha-<short>` and `:main`, with
the same guarantees as the other nine:
- built, then scanned for secrets in every layer, then pushed;
- a `sha-` tag that is never rewritten;
- the release's "every name exists" check covers it.

The publish job also waits for the storefront's own tests, so a storefront whose tests failed is never
published.

**Why this priority**: An image nobody publishes is not something a person can pull; and publishing without
waiting for the tests would publish a broken storefront.

**Independent Test**: After a merge, the `publish` job lists `storefront` among its services and the final
existence check counts ten names.

**Acceptance Scenarios**:

1. **Given** a merge to `main` whose checks pass, **When** `publish` runs, **Then** it builds, scans, pushes and
   confirms `ecommerce-storefront` under the same `sha-` rules as the others.
2. **Given** the `client` job failed, **When** the workflow runs, **Then** `publish` does not run.

---

### US3 - An image that builds but serves nothing fails CI (Priority: P2)

A CI check builds the image, starts it against a stand-in gateway, and asserts:
- the app is served;
- a deep link is answered with the app;
- `/api` is forwarded, path and all;
- `index.html` is not cached and an asset is;
- a 2 MB upload is not refused on the way.

The same script runs locally.

**Why this priority**: `docker build` succeeding says only that the bundle compiled. It comes after US1 and
US2 because it guards them.

**Independent Test**: `.github/scripts/verify-storefront-image.sh <image>`; then break the template (remove the
SPA fallback) and see it fail.

**Acceptance Scenarios**:

1. **Given** a correct image, **When** the script runs, **Then** every check passes.
2. **Given** an image whose SPA fallback, body limit, asset rule or cache headers were removed, **When** the
   script runs, **Then** it fails and names the check.

### Edge Cases

- **Deep links.** `/orders/<id>` is a route only the browser-side router knows; the server must answer it with
  the app.
- **Upload size.** nginx refuses bodies over 1 MB by default - below Catalog's 2 MB - and would answer 413
  before Catalog saw the request.
- **Caching.** A cached `index.html` would pin a browser to an old release; an uncached asset would be fetched
  on every load.
- **The Secure cookie over a LAN IP.** Browsers accept a `Secure` cookie over plain HTTP only from
  `localhost`, so opening the storefront by LAN IP breaks sign-in persistence. Documented, not solved.
- **The gateway restarts.** nginx resolved its name once at start; compose gives a restarted gateway the same
  name, so the address stays valid.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The gateway's address is read when the container starts (`GATEWAY_URL`). One image runs
  against any gateway.
- **FR-002**: The image runs as a non-root user, like the service images.
- **FR-003**: The image contains no `.env` and no secret. The existing layer scanner is run on it.
- **FR-004**: The storefront's port (8088 on the host) collides with nothing already in compose or in
  `start-dev`.
- **FR-005**: The body limit on the way to the gateway stays above Catalog's `ProductImageKey.MaxBytes`.
- **FR-006**: `publish` waits for the `client` job, and the storefront joins the same build, scan, push and
  existence checks as the nine server images.

### Key Entities

- **Storefront image**: `ghcr.io/jessiicamaru/ecommerce-storefront`, tags `sha-<short>` (immutable) and `main`
  (moves). Built from `client/` alone.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the full compose stack up, a person can sign in, browse and add to the cart through
  `http://localhost:8088` with no Vite dev server running.
- **SC-002**: The verify script's checks all pass on the built image, and each of four deliberate breakages of
  the template makes it fail.
- **SC-003**: The secret scanner finds nothing in any layer of the image, run with the real `server/.env`
  present.
- **SC-004**: A merge publishes ten images, and the release's existence check counts ten.

## Out of scope

- TLS and a public hostname: that is deployment, which stays deferred.
- Serving the storefront from the gateway itself. Rejected: it would couple a .NET image to a Node build.

## Assumptions

- The refresh cookie is `Secure`. Browsers accept that over plain HTTP on `localhost` only, so over a LAN
  IP the storefront needs TLS in front of it. This is documented, not solved here.
- The storefront keeps calling `/api` on its own origin; nothing in the client changes.
