# Implementation Plan: Storefront image

**Branch**: `051-storefront-image` | **Spec**: [spec.md](spec.md) | **Issue**: #132

## Technical context

- Client: Vite 7 + React. `npm run build` runs `tsc -b && vite build`. Relative `/api` calls are
  forwarded by the dev proxy in `vite.config.ts`.
- Nine images: one `server/Dockerfile` with a `PROJECT` argument, and the build context `server/`.
- The `publish` job builds and scans every image, then pushes with a `sha-` existence check, then asks the
  registry whether every name exists. It `needs: [build, auth-smoke, saga-e2e]`, but not `client`.
- Catalog accepts an image up to `ProductImageKey.MaxBytes` = 2 MB. nginx refuses bodies over 1 MB by
  default.

## Decisions

**D1 - nginx, unprivileged, templated at start.**
- The image is `nginxinc/nginx-unprivileged`, pinned. It runs as uid 101 on port 8080, as the service
  images bind 8080.
- Its entrypoint runs `envsubst` over `/etc/nginx/templates/*.template` for **defined** variables only.
  So `${GATEWAY_URL}` is filled at start, while nginx's own `$uri` and `$host` stay as they are.
- Rejected:
  - baking the address in at build time (one image per environment);
  - a Node server (a runtime for static files);
  - serving from the YARP gateway (couples a .NET image to a Node build).

**D2 - Build context `client/`, and the image runs `vite build` only.** `npm run build` also runs
`tsc -b` over `src/`, test files included. One test imports `server/.../notification-kinds.json`
(specs/048), so a `client/`-only context cannot type-check. Type-checking and tests belong to CI's
`client` job, which `publish` now also needs. The image builds exactly the bundle that job built.
Rejected: the repository root as the context, which would expose the whole repository, `server/.env`'s
directory included, to a build that needs one folder.

**D3 - `client_max_body_size 3m`.** That is Catalog's 2 MB plus room for the multipart envelope. The
limit must stay above `ProductImageKey.MaxBytes`, and the CI check uploads 2 MB through it.

**D4 - Caching.**
- `/assets/`: `Cache-Control: public, max-age=31536000, immutable`. Vite names these files by content
  hash.
- `index.html` and the SPA fallback: `no-cache`.

**D5 - Host port 8088.** 8080 is used inside every container and is a common default. 5000–5063 are the
services, 5050 is pgAdmin and 5173 is Vite dev. 8088 collides with none of them.

**D6 - Upstream resolved at start, with the gateway healthy first.** `depends_on: gateway: service_healthy`.
nginx resolves `gateway` once at start. That is acceptable in compose, which gives a restarted gateway
the same name, and it is recorded as a limit.

**D7 - The CI check is a script**, `.github/scripts/verify-storefront-image.sh`, that runs locally too.
- It starts a stand-in gateway on a user network: `python -m http.server` serving a known file under
  `/api/`, plus an echo of the path.
- It runs the image with `GATEWAY_URL` pointing at the stand-in.
- It curls: `/`, a deep link, `/api/...`, the cache headers on `index.html` and on one asset, and a 2 MB
  POST that must not come back 413.
- It runs in the `client` job, after the build.

## Constitution check

- I (autonomy): the storefront still talks only to the gateway. Pass.
- Release immutability (specs/006, specs/008): the storefront joins the same loop, the same existence
  check and the same completeness check. Pass.
- Secrets: the image is scanned by the same layer scanner, and `client/.dockerignore` excludes `.env*`.
  Pass.
- V (evidence): the verify script is the evidence. It is run locally and in CI, with a negative control
  (the SPA fallback removed must fail it).

## Files

- `client/Dockerfile`, `client/.dockerignore`, `client/nginx/default.conf.template`
- `server/docker-compose.app.yml`: the `storefront` service
- `.github/workflows/ci.yml`:
  - the `client` job builds and verifies the image;
  - `publish` needs `client`;
  - the storefront is built, scanned, pushed and confirmed.
- `.github/scripts/verify-storefront-image.sh`
- Docs:
  - `docs/infrastructure/running-in-containers.md`
  - `docs/architecture/storefront.md`
  - `docs/guides/getting-started.md`
  - `CLAUDE.md`
  - `docs/project/*`
