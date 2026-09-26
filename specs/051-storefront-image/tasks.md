---
description: "Task list for Storefront image"
---

# Tasks: Storefront image

> Completed on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the verify script was written first (T001) and is the feature's test; negative controls prove it
can fail.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The check (US3)

- [X] T001 [US3] `.github/scripts/verify-storefront-image.sh` first: stand-in gateway, the six assertions
- [X] T007 [US3] Make the script report a missing header instead of dying silently under `set -e -o pipefail` (`header()` ends its `grep` with `|| true`) in `.github/scripts/verify-storefront-image.sh` - found by the cache-header negative control

## Phase 2: The image (US1)

- [X] T002 [US1] `client/Dockerfile`, `client/.dockerignore`, `client/nginx/default.conf.template`; the script passes locally
- [X] T003 [US1] `storefront` service in `server/docker-compose.app.yml`; bring it up against the real stack and sign in / browse / add to cart through it

## Phase 3: Publishing (US2)

- [X] T004 [US2] `.github/workflows/ci.yml`: `client` job builds and verifies the image; `publish` needs `client`; storefront built, scanned, pushed, confirmed

## Phase 4: Verification and docs

- [X] T005 Negative control: remove the SPA fallback, the script must fail; scan the image for secrets
- [X] T008 [P] Further negative controls: body limit removed, assets falling back to `index.html`, cache headers removed - each red, then restored
- [X] T009 [P] Headless Chrome against `http://localhost:8088`: home page with product links, a product page with photograph, variants, prices and stock
- [X] T006 Docs: `docs/infrastructure/running-in-containers.md`, `docs/architecture/storefront.md`, `docs/guides/getting-started.md`, `CLAUDE.md`, `docs/project/*`
- [X] T010 [P] `docs/testing/testing-strategy.md` and decision 42 in `docs/project/decisions.md`
- [X] T011 Merged as #133 (2026-09-24), closing #132

## Verification recorded in #133

- The script: 9 of 9 checks pass locally (T001 said "six assertions"; the script grew to nine - non-root and
  the missing-asset 404 were added, and the cache check covers `index.html`, a deep link and an asset).
- Negative controls: SPA fallback removed - 2 red; body limit removed - the upload red; assets fall back to
  `index.html` - red; cache headers removed - 3 red.
- Secret scan clean in every layer with the real `server/.env`.
- Real stack through :8088: admin sign-in 200 with the cookie stored; refresh 200; catalogue read with
  `Content-Language: vi`, `Vary`, `X-Currency` and 20 products; a deep link 200; a throwaway customer adds a
  variant to the cart, 204, one line.
- **Not verified before merge:** the `publish` job, which runs only on `main`. The CI run on the merge commit
  shows it succeeded.

## Notes

T007-T011 were added on 2026-09-27 from the pull request; the work was part of #133 but had no task line.
