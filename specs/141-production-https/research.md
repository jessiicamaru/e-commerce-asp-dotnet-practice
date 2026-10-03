# Research: A production stack served over HTTPS

## D1 - An overlay, not a second compose file

**Decision**: `docker-compose.prod.yml` overlays `docker-compose.yml` and `docker-compose.app.yml`. It replaces `build`
with the published `image`, removes published ports with `!reset`, and adds Caddy.

**Rationale**: Every service's environment, health check and dependency is already written once. A separate
production file would be a copy that drifts: the next setting added in development would be missing in production.

**Alternatives rejected**:
- A standalone production compose file: duplicated configuration.
- Kubernetes manifests: an orchestrator a one-server thesis deployment does not need.

## D2 - Caddy, for certificates that renew themselves

**Decision**: Caddy terminates TLS for both domains and proxies to the apps' nginx. Locally it issues certificates from
its own CA for `*.localhost`.

**Rationale**: Automatic ACME issuance and renewal with a configuration of a few lines. The same file works locally
and on a server, changing only the domain names.

**Alternatives rejected**: nginx with certbot (a renewal job to run and watch); Traefik (labels on every service for
two routes).

## D3 - Count the hops, do not move the trust

**Decision**: The gateway reads `GATEWAY_FORWARD_LIMIT` hops (default 1) and trusts Caddy's fixed address as well as
the apps' nginx.

**Rationale**: With one hop the gateway would key every visitor on Caddy's address, and one person's wrong passwords
would pause everybody's sign-in (specs/062). Caddy replaces any `X-Forwarded-For` a visitor sends, so the chain
visitor → Caddy → nginx → gateway has exactly two trusted hops.

**Alternatives rejected**: nginx's `real_ip` module (it changes the apps' image for one deployment), or trusting every
peer (a forged header per request then bypasses the limit - the defect specs/062 fixed).

## D4 - SMTP that a provider accepts

**Decision**: Optional username and password, STARTTLS (`SMTP_TLS`), and the sender address (`SMTP_FROM`). Both
credentials or neither, checked at startup.

**Rationale**: No hosted mail service accepts unauthenticated plain SMTP. Mailpit in development still needs none of
it.
