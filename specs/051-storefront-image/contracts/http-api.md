# Contract: the storefront container

> Written on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**Feature**: [spec.md](../spec.md)

What the image serves over HTTP, how it is configured, and the names it is published under. No backend
endpoint, message or gRPC contract changed; the storefront keeps calling the gateway's API exactly as it
did through Vite's dev proxy.

## Configuration

| Setting | Where | Default | Meaning |
| :--- | :--- | :--- | :--- |
| `GATEWAY_URL` | environment, read at container start | `http://gateway:8080` | Where `/api/` is forwarded. Compose sets `http://gateway:8080` |
| Listen port | fixed | `8080` inside | Compose publishes it as **`8088`** on the host |
| User | image | uid `101` | nginx-unprivileged's user; not root |

## HTTP surface (`client/nginx/default.conf.template`)

| Path | Answer | Headers |
| :--- | :--- | :--- |
| `/api/**` | Proxied to `${GATEWAY_URL}` with the path and query unchanged, HTTP/1.1; status, headers and body are the gateway's | Adds `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host` upstream |
| `/assets/<file>` | The file, or **404** if it does not exist - never `index.html` | `Cache-Control: public, max-age=31536000, immutable` |
| any other path | The file if it exists, else `index.html` (200) - the client-side router takes it from there | `Cache-Control: no-cache` |

- Request bodies up to **3 MB** (`client_max_body_size 3m`), above Catalog's 2 MB `ProductImageKey.MaxBytes`.
  A larger body is refused by nginx with 413.
- Health: compose checks `wget -q -O /dev/null http://127.0.0.1:8080/` every 10 s.

## Published names

On a merge to `main` whose checks pass (and since this feature, only when the `client` job passed too):

```text
ghcr.io/jessiicamaru/ecommerce-storefront:sha-<short-sha>   # immutable; skipped if it already resolves
ghcr.io/jessiicamaru/ecommerce-storefront:main              # moves
```

Built from `client/` with `client/Dockerfile`, scanned by `verify-image-has-no-secrets.sh` before any push, and
counted by the release's "every name exists" check (`SERVICES="... gateway storefront"`).

## Compose

`server/docker-compose.app.yml`, service `storefront`, container `ecommerce-storefront`, build context
`../client`, `restart: unless-stopped`, `depends_on: gateway: condition: service_healthy`.
