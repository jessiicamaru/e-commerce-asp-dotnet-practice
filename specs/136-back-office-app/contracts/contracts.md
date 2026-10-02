# Contracts: A back office for staff, with its own sign-in

No HTTP, message or gRPC change. The back office uses the existing endpoints: `POST /api/auth/login`,
`/login/two-factor`, `/refresh` and `/logout`.

## Runtime

| What | Development | Compose |
| :-- | :-- | :-- |
| Back office | `http://portal.localhost:5174` | `http://portal.localhost:8089` (container `ecommerce-back-office`, `172.30.10.11` on `edge`) |
| Storefront | `http://localhost:5173` | `http://localhost:8088` (`172.30.10.10`) |
| Gateway's trusted proxies | - | `172.30.10.10,172.30.10.11` |

## Image

`ghcr.io/jessiicamaru/ecommerce-back-office:sha-<short-sha>` (and `:main`), built by `docker build --build-arg
APP=back-office client`.
