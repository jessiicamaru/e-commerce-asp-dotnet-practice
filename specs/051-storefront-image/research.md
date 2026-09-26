# Research: Storefront image

> Written on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

Seven decisions. The pull request records D1, D2, D5 and D6 as decided on the user's behalf; the user chose
only to do this before the remaining P0 defects.

---

## D1 - nginx, unprivileged, templated at start

**Decision**: The runtime stage is `nginxinc/nginx-unprivileged:1.27-alpine`, running as uid 101 on port
8080. `client/nginx/default.conf.template` is copied to `/etc/nginx/templates/`, and the image's entrypoint
renders it with `envsubst` - for **defined** variables only - before nginx starts. `GATEWAY_URL` defaults to
`http://gateway:8080`.

**Rationale**: One image runs against any gateway (FR-001), and nginx's own `$uri` and `$host` are left
alone because they are not defined in the environment. uid 101 on 8080 matches the service images (FR-002).

**Alternatives considered**:

- **Bake the gateway address in at build time.** Rejected: one image per environment, which defeats an
  immutable `sha-` image.
- **A Node server (`vite preview`, Express).** Rejected: a whole runtime to serve static files.
- **Serve the bundle from the YARP gateway.** Rejected: it couples a .NET image to a Node build, and the
  gateway would change whenever the storefront did.

---

## D2 - Build context `client/`, and the image runs `vite build` only

**Decision**: `docker build ... client`; the build stage runs `npm ci` then `npx vite build`.

**Rationale**: `npm run build` also runs `tsc -b` over `src/`, tests included, and one test imports
`server/.../notification-kinds.json` (specs/048, #119), which a `client/`-only context does not contain.
Type-checking and tests are the `client` job's, which `publish` now needs, so the image builds exactly the
bundle that job checked.

**Alternatives considered**:

- **The repository root as the context.** Rejected: it would expose the whole repository, `server/.env`'s
  directory included, to a build that needs one folder.
- **Copy the JSON file into `client/` for the build.** Rejected: a second copy of a file whose point is to be
  declared once.

---

## D3 - `client_max_body_size 3m`

**Decision**: 3 MB on the whole server block.

**Rationale**: Catalog accepts a photograph up to 2 MB (`ProductImageKey.MaxBytes`); nginx's default 1 MB
would answer 413 before Catalog saw it. 3 MB leaves room for the multipart envelope. The verify script
uploads exactly 2,097,152 bytes and checks the stand-in received that many.

**Alternatives considered**:

- **nginx's default 1 MB.** Rejected: it answers 413 below Catalog's limit - the negative control "body
  limit removed" turned the 2 MB upload check red.
- No other value is recorded as considered.

---

## D4 - Caching

**Decision**: `/assets/` - `Cache-Control: public, max-age=31536000, immutable` and `try_files $uri =404`;
everything else - `no-cache` and `try_files $uri /index.html`.

**Rationale**: Vite names assets by content hash, so a file never changes under its name. `index.html` names
the current hashes, so it must be revalidated on every load for a new release to be picked up. A missing
asset must be a 404: falling back to `index.html` would hand the browser HTML where it expects JavaScript.

**Alternatives considered**:

- **No cache headers.** Rejected: the negative control "cache headers removed" made three checks red.
- **Assets falling back to `index.html`.** Rejected for the missing-asset reason; the negative control
  "assets fall back to `index.html`" made the 404 check red.

---

## D5 - Host port 8088

**Decision**: `"8088:8080"` in `docker-compose.app.yml`.

**Rationale**: 8080 is used inside every container and is a common default; 5000-5063 are the services, 5050
is pgAdmin, 5173 is Vite dev. 8088 collides with none of them (FR-004).

**Alternatives considered**:

- **8080.** Rejected: used inside every container and a common default.
- No other port is recorded as considered.

---

## D6 - Upstream resolved at start, with the gateway healthy first

**Decision**: `proxy_pass ${GATEWAY_URL}` with no resolver; `depends_on: gateway: condition: service_healthy`.

**Rationale**: nginx resolves `gateway` once when it starts. In compose a restarted gateway keeps its name,
and waiting for it to be healthy means the name resolves. Recorded as a limit.

**Alternatives considered**:

- None recorded. The limit is written down in the plan, the compose file and the pull request instead.

---

## D7 - The check is a script that runs locally and in CI

**Decision**: `.github/scripts/verify-storefront-image.sh <image>` creates a user network, starts a stand-in
gateway (`python:3.12-alpine` running a small `BaseHTTPRequestHandler` that answers every method and path with
`stand-in <METHOD> <path> <body length>`), starts the image with `GATEWAY_URL` pointing at it, and checks:
non-root user; `/` is the app (`id="root"`); a deep link is the app with 200; a missing asset is 404; `/api`
arrives with path and query; a 2 MB POST arrives whole; `index.html` and a deep link are `no-cache`; a hashed
asset is 200 and `immutable`. It runs in the `client` job after the image build.

**Rationale**: A shell script can build, run and curl a container, and the same file runs on a developer's
machine. The stand-in's echo makes a forwarded call unmistakable.

**Alternatives considered**:

- **Point it at the real stack.** Rejected: the `client` job has no backend, and the real stack would make a
  storefront check depend on eight services.
- **`python -m http.server` serving a file.** The first plan's idea; replaced by the echo so the path, query,
  method and body length can all be asserted.

The negative controls found a bug in the script itself: a missing header made `grep` exit 1 under
`set -e -o pipefail`, and the script died without a word. `header()` now ends its `grep` with `|| true` and the
script names each missing header.
