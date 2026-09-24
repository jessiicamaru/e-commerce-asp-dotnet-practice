# Tasks: Storefront image

- [X] T001 [US3] `.github/scripts/verify-storefront-image.sh` first: stand-in gateway, the six assertions
- [X] T002 [US1] `client/Dockerfile`, `client/.dockerignore`, `client/nginx/default.conf.template`; the script passes locally
- [X] T003 [US1] `storefront` service in `server/docker-compose.app.yml`; bring it up against the real stack and sign in / browse / add to cart through it
- [X] T004 [US2] `.github/workflows/ci.yml`: `client` job builds and verifies the image; `publish` needs `client`; storefront built, scanned, pushed, confirmed
- [X] T005 Negative control: remove the SPA fallback, the script must fail; scan the image for secrets
- [ ] T006 Docs: `docs/infrastructure/running-in-containers.md`, `docs/architecture/storefront.md`, `docs/guides/getting-started.md`, `CLAUDE.md`, `docs/project/*`
