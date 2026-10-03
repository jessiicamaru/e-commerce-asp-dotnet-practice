# Quickstart: A production stack served over HTTPS

## Locally, over HTTPS

```bash
cd server
cp deploy/production.env.example deploy/local.env     # fill the secrets; SHOP_DOMAIN=shop.localhost, PORTAL_DOMAIN=portal.shop.localhost
RELEASE=sha-<tag> docker compose --env-file deploy/local.env -f docker-compose.yml -f docker-compose.app.yml -f docker-compose.prod.yml up -d
```

Expected: `https://shop.localhost` and `https://portal.shop.localhost` serve the apps (trust Caddy's local CA once,
or accept the warning), and `docker compose ps` shows only Caddy publishing ports.

## Tests

```bash
cd server && dotnet test tests/Ecommerce.ApiGateway.Tests && DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter Smtp
```
