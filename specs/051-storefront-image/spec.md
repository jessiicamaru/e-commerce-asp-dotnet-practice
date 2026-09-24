# Feature Specification: Storefront image

**Feature Branch**: `051-storefront-image` | **Created**: 2026-09-24 | **Issue**: #132

## Why

On every merge to `main`, each service and the gateway are built, scanned for secrets and published as an
image, nine in all. The storefront is linted, tested and built, and then its `dist/` is thrown away. It
also could not be served as built: it calls `/api` on its own origin, and only Vite's dev proxy forwards
that to the gateway. So the one way to see the interface is `npm run dev` on a developer's machine.
`docker compose ... up` brings up the whole backend and nothing to click.

The user prioritised this ahead of the remaining P0 defects. It is what makes the whole system,
interface included, runnable from images and demonstrable for the project report.

## User Scenarios

### US1 - The whole system from images (P1)

Somebody runs `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build` and opens
`http://localhost:8088`. The storefront loads. Signing in, browsing, the cart and checkout all work,
because `/api` reaches the gateway on the same origin, exactly as in development.

**Acceptance**
1. `/` serves the app.
2. A deep link such as `/orders/<id>` or `/shop/products/<id>`, opened directly or reloaded, serves the
   app, not a 404.
3. `/api/...` is answered by the gateway, headers and status included. The HttpOnly refresh cookie
   keeps working, because the origin is one.
4. A product photograph up to Catalog's 2 MB limit uploads through it. Nothing in between refuses it
   first.
5. `index.html` is never cached. Hashed assets under `/assets/` are cached for a year and marked
   immutable, so a new release is picked up at the next load and an old one's files are never stale.

### US2 - Published like everything else (P1)

A merge to `main` publishes `ghcr.io/jessiicamaru/ecommerce-storefront:sha-<short>` and `:main`, with
the same guarantees as the other nine:
- built, then scanned for secrets in every layer, then pushed;
- a `sha-` tag that is never rewritten;
- the release's "every name exists" check covers it.

The publish job also waits for the storefront's own tests, so a storefront whose tests failed is never
published.

### US3 - An image that builds but serves nothing fails CI (P2)

A CI check builds the image, starts it against a stand-in gateway, and asserts:
- the app is served;
- a deep link is answered with the app;
- `/api` is forwarded, path and all;
- `index.html` is not cached and an asset is;
- a 2 MB upload is not refused on the way.

The same script runs locally.

## Requirements

- **FR-001**: The gateway's address is read when the container starts (`GATEWAY_URL`). One image runs
  against any gateway.
- **FR-002**: The image runs as a non-root user, like the service images.
- **FR-003**: The image contains no `.env` and no secret. The existing layer scanner is run on it.
- **FR-004**: The storefront's port (8088 on the host) collides with nothing already in compose or in
  `start-dev`.

## Out of scope

- TLS and a public hostname: that is deployment, which stays deferred.
- Serving the storefront from the gateway itself. Rejected: it would couple a .NET image to a Node build.

## Assumptions

- The refresh cookie is `Secure`. Browsers accept that over plain HTTP on `localhost` only, so over a LAN
  IP the storefront needs TLS in front of it. This is documented, not solved here.
