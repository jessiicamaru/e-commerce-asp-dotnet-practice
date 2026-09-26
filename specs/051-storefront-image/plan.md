# Implementation Plan: Storefront image

> Completed on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Branch**: `051-storefront-image` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #132

## Summary

A two-stage `client/Dockerfile` - Node builds the bundle with `vite build`, an unprivileged nginx serves it -
with an nginx template that forwards `/api` to `GATEWAY_URL`, filled in when the container starts. Compose
gains a `storefront` service on host port 8088. CI's `client` job builds the image and runs a new
`verify-storefront-image.sh` against a stand-in gateway; `publish` now needs `client` and publishes
`ecommerce-storefront` as the tenth image under the existing `sha-` rules.

## Technical context

- Client: Vite 7 + React. `npm run build` runs `tsc -b && vite build`. Relative `/api` calls are
  forwarded by the dev proxy in `vite.config.ts`.
- Nine images: one `server/Dockerfile` with a `PROJECT` argument, and the build context `server/`.
- The `publish` job builds and scans every image, then pushes with a `sha-` existence check, then asks the
  registry whether every name exists. It `needs: [build, auth-smoke, saga-e2e]`, but not `client`.
- Catalog accepts an image up to `ProductImageKey.MaxBytes` = 2 MB. nginx refuses bodies over 1 MB by
  default.

**Language/Version**: TypeScript / React 19 built by Vite 7; nginx configuration; Bash; GitHub Actions YAML

**Primary Dependencies**: `node:22-alpine` (build stage), `nginxinc/nginx-unprivileged:1.27-alpine` (runtime
stage), `python:3.12-alpine` (the verify script's stand-in gateway)

**Storage**: none

**Testing**: `verify-storefront-image.sh` against the built image, with four negative controls; the existing
`verify-image-has-no-secrets.sh`; by hand against the real compose stack and in headless Chrome

**Target Platform**: Linux container, uid 101, port 8080 inside, 8088 on the host

**Constraints**: one origin (no CORS, a same-origin refresh cookie); no secret in any layer; one image for
any gateway

**Scale/Scope**: static files; the image is about 20 MB (from the pull request)

## Decisions

Recorded in full, with alternatives, in [research.md](research.md).

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

> **Correction (2026-09-27, from the code at the merge):** the stand-in is not `python -m http.server` serving
> a file. It is a few lines of Python on `python:3.12-alpine` - a `BaseHTTPRequestHandler` that answers every
> method and path with `stand-in <METHOD> <path> <body length>` - so a forwarded call cannot be mistaken for
> the storefront answering itself, and the 2 MB upload's length is checked on arrival. The script also checks
> that the image runs as non-root and that a missing asset is a 404: nine checks in all.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- I (autonomy): the storefront still talks only to the gateway. Pass.
- Release immutability (specs/006, specs/008): the storefront joins the same loop, the same existence
  check and the same completeness check. Pass.
- Secrets: the image is scanned by the same layer scanner, and `client/.dockerignore` excludes `.env*`.
  Pass.
- V (evidence): the verify script is the evidence. It is run locally and in CI, with a negative control
  (the SPA fallback removed must fail it).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The storefront reaches the backend only through the gateway, as in development; it reads no database and adds no shared code |
| **II. Clean Architecture Layering** | **Pass, not engaged.** No .NET project changed; the storefront's own conventions are untouched |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, not engaged.** No write path and no message |
| **IV. Identity Comes From the Token** | **Pass.** nginx forwards requests unchanged; the refresh cookie stays HttpOnly and same-origin, and nothing in the proxy adds or reads identity |
| **V. Evidence Over Assumption** | **Pass.** The verify script exercises the built image over HTTP; four negative controls each made it fail (and one exposed a bug in the script itself, fixed); the secret scan ran with the real `server/.env`; the real stack was exercised through :8088. The `publish` job itself was **not** verified before merge, and the pull request says so |

Also checked against the constitution's release rules (specs/006, specs/008): the storefront's `sha-` tag goes
through the same "already exists, skip" check, and the completeness check counts it.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/051-storefront-image/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D7 with rejected alternatives
├── data-model.md        # No data changed
├── quickstart.md        # Build, verify, compose, negative controls
├── contracts/
│   └── http-api.md      # What the container serves, its configuration, the published names
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

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
  - `docs/testing/testing-strategy.md` (touched by the pull request, not listed in the first plan)

**Structure Decision**: the storefront gets its own Dockerfile beside its source rather than a tenth
`PROJECT` in `server/Dockerfile`, because its build shares nothing with the .NET images (D1, D2).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- TLS and a public hostname; until then the storefront works only on `localhost` because of the `Secure`
  refresh cookie.
- nginx resolves the gateway once at start (D6); outside compose, a gateway whose address changes needs the
  storefront restarted.
- The first publish of the tenth image could be confirmed only by the `publish` job after the merge, not
  before it. The CI run on the merge commit (`push`, run 35968719395) shows `Publish images` succeeded; its log
  was not read for this record.
