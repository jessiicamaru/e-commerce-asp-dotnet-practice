# Quickstart: Validating the storefront image

> Written on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

Docker, and the repository root as the working directory. For scenario 3, `server/.env` filled in as for any
compose run.

## Scenario 1 - The image serves the storefront (US3, SC-002)

```bash
docker build -t ecommerce-storefront client
.github/scripts/verify-storefront-image.sh ecommerce-storefront
```

**Expected**: nine `ok` lines - non-root (101); `/` is the app; a deep link is the app; a missing asset is
404; `/api` forwarded with its path and query; a 2 MB upload reaches the gateway; `index.html` and a deep link
`no-cache`; a hashed asset `immutable` - then "The storefront image serves the storefront." The pull request
ran exactly this and all nine passed.

## Scenario 2 - Negative controls (SC-002)

Edit `client/nginx/default.conf.template`, rebuild, rerun the script, restore. The pull request's results:

| Change | Expected result |
| :-- | :-- |
| SPA fallback removed | 2 red |
| `client_max_body_size` removed | the 2 MB upload is red |
| `/assets/` falls back to `index.html` | red |
| Cache headers removed | 3 red |

## Scenario 3 - No secret in any layer (FR-003, SC-003)

```bash
.github/scripts/verify-image-has-no-secrets.sh ecommerce-storefront
```

**Expected**: clean, with the real `server/.env` present on disk (the pull request ran it so).

## Scenario 4 - The whole system through :8088 (US1, SC-001)

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8088/shop/products/x                 # deep link
curl -s -D - -o /dev/null "http://localhost:8088/api/products?lang=vi" | grep -i 'content-language\|vary\|x-currency'
```

**Expected**: `200`; `Content-Language`, `Vary` and `X-Currency` passed through from Catalog. Then in a browser
on `http://localhost:8088` (not a LAN IP - the refresh cookie is `Secure`): sign in as the administrator (200,
the refresh cookie stored), reload (the session is restored by `POST /api/auth/refresh`), sign up a throwaway
customer and add a variant to the cart (204; the cart has one line).

The pull request recorded these results against the real stack, including 20 products from the catalogue read,
and in headless Chrome 13 product links on the home page and a product page with photograph, variants, prices
and stock; the only 4xx responses were the expected 401s from `/api/auth/refresh` while signed out.

## Scenario 5 - Published (US2, SC-004)

After a merge to `main`, the `Publish images` job builds, scans, pushes and confirms ten names.

```bash
docker pull ghcr.io/jessiicamaru/ecommerce-storefront:main
```

**Expected**: the image pulls; `docker run -p 8088:8080 -e GATEWAY_URL=http://<gateway> ...` serves it.
