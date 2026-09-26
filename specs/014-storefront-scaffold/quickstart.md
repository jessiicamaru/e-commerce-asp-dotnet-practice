# Quickstart: A Storefront That Builds and Reaches the Gateway

> Written on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull request
> and docs/architecture/storefront.md (the storefront has no page of its own under docs/features/).

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

Node 22 (what CI uses) and the backend running behind the gateway:

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

## Scenario 1 - It builds and lints (SC-001, FR-001)

```bash
cd client
npm ci
npm run lint        # oxlint
npm run build       # tsc -b, then vite build - what CI runs
```

**Expected**: both clean. The pull request recorded both clean.

## Scenario 2 - `/api` reaches the gateway, every service answers (SC-002, US1)

```bash
cd client && npm run dev        # http://localhost:5173
for s in identity catalog order inventory payment cart; do
  printf '%-10s ' "$s"; curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5173/api/$s/health"
done
```

Then open <http://localhost:5173/status>.

**Expected**: six `200`s, and six "up" on the page. The pull request recorded the page and all six
routes answering 200 through `localhost:5173`. Whether the page itself was looked at in a browser, as
opposed to fetched, is not recorded.

## Scenario 3 - A different gateway (FR-006)

```bash
GATEWAY_URL=http://localhost:5999 npm run dev
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5173/api/catalog/health
```

**Expected**: a proxy error (5xx) - the request went to `:5999`, where nothing listens. Not recorded as
run.

## Scenario 4 - A down service reads "down" (US1 scenario 2)

```bash
docker stop $(docker ps -qf name=payment)     # the Payment container, whatever compose named it
```

Reload `/status`. **Expected**: payment reads `down (502)` or similar; the others read "up". Restart it
afterwards. Not recorded as run.

## Scenario 5 - CI

Open any pull request. **Expected**: a job named **Storefront build** runs beside the backend jobs and
passes. On #42 it ran for the first time.
