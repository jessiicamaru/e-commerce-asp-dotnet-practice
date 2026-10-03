# Contracts: A production stack served over HTTPS

## Configuration

| Variable | Read by | Default | Production |
| :-- | :-- | :-- | :-- |
| `RELEASE` | the overlay | none - required | `sha-<short-sha>` |
| `SHOP_DOMAIN`, `PORTAL_DOMAIN` | Caddy; derived URLs | none - required | the two hosts |
| `ACME_EMAIL` | Caddy | none | the operator's email |
| `GATEWAY_FORWARD_LIMIT` | gateway | `1` | `2` |
| `GATEWAY_TRUSTED_PROXIES` | gateway | as before | adds Caddy, `172.30.10.2` |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | Identity | none | the provider's |
| `SMTP_TLS` | Identity | `false` | `true` |
| `SMTP_FROM` | Identity | `e-commerce <no-reply@ecommerce.local>` | the shop's sender |

No HTTP, message or gRPC change.
