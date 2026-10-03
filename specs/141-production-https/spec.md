# Feature Specification: A production stack served over HTTPS

**Feature Branch**: `feat/287-production-https` | **Created**: 2026-10-03 | **Issue**: #287

**Status**: Draft

**Input**: Issue #287, "a production stack served over HTTPS" - the first of the steps that make the project ready to
defend (backlog, "ready to defend").

## Why

Every image is published, scanned and named by commit, but the shop runs only on a developer's machine. Off
`localhost` it cannot even sign anybody in: Identity's refresh cookie is `Secure`, and browsers accept a `Secure`
cookie over plain HTTP only from `localhost`. A thesis about this system needs it running somewhere real, on the two
hosts ADR-003 designed - a storefront and a back office - over HTTPS.

## User Scenarios & Testing *(mandatory)*

### US1 - The shop runs from its published images, over HTTPS (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a server with Docker and the release's env file, **When** the production stack is started with a `sha-` tag, **Then** the storefront answers on `https://SHOP_DOMAIN` and the back office on `https://PORTAL_DOMAIN`, with certificates obtained automatically.
2. **Given** the stack, **Then** nothing but Caddy is reachable from outside: no database, broker, service, Seq or gateway port.
3. **Given** a release name that is not a `sha-` tag, or a missing secret, **Then** the stack refuses to start and says which.

---

### US2 - The rate limits still count people, behind one more proxy (Priority: P1)

**Acceptance Scenarios**:

1. **Given** Caddy in front of nginx in front of the gateway, **Then** the gateway counts sign-in attempts per visitor, not per proxy: it reads exactly as many forwarded hops as there are trusted proxies (`GATEWAY_FORWARD_LIMIT`).
2. **Given** a visitor writing their own `X-Forwarded-For`, **Then** it is ignored. Caddy discards it, and the gateway trusts only its proxies' hops.

---

### US3 - Email reaches real inboxes (Priority: P2)

**Acceptance Scenarios**:

1. **Given** `SMTP_USERNAME`, `SMTP_PASSWORD` and `SMTP_TLS=true`, **Then** Identity signs in to the mail server over STARTTLS. Without them it speaks plain SMTP to Mailpit, as before.
2. **Given** a username without a password (or the reverse), **Then** Identity refuses to start.

### Edge Cases

- In development nothing changes. Ports stay published, Mailpit catches mail, and `GATEWAY_FORWARD_LIMIT` defaults to 1.
- Seq and pgAdmin are not published in production. Seq is reached over an SSH tunnel; pgAdmin is not started.
- The administrator's TOTP secret is never seeded in production (`ADMIN_TOTP_SECRET` empty). The first administrator sets up two-factor sign-in on the storefront, as any staff from before specs/110 does.
- Local verification uses the same overlay with `SHOP_DOMAIN=shop.localhost`, for which Caddy issues certificates from its own CA.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `server/docker-compose.prod.yml`, an overlay on the two existing files. It sets `image: ghcr.io/.../ecommerce-<svc>:${RELEASE}`, removes every `build` and every published port, and adds Caddy.
- **FR-002**: `server/deploy/Caddyfile`: `SHOP_DOMAIN` to the storefront's nginx and `PORTAL_DOMAIN` to the back office's, HTTPS automatic.
- **FR-003**: `server/deploy/production.env.example`: every variable, and how to generate each secret.
- **FR-004**: The gateway's `GATEWAY_FORWARD_LIMIT` (1 to 5, default 1). Production trusts Caddy's address too, with a limit of 2.
- **FR-005**: Identity's SMTP: `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_TLS`, `SMTP_FROM`, validated at startup.
- **FR-006**: Verified locally over HTTPS. A guide: `docs/infrastructure/production.md`.

## Success Criteria *(mandatory)*

- **SC-001**: With the overlay on this machine, `https://shop.localhost` and `https://portal.shop.localhost` serve the apps, a person signs in on both, and the Playwright flows pass against them.
- **SC-002**: Gateway tests show two hops resolving to the visitor, and a forged header ignored.

## Assumptions

- One server, Docker Compose. Orchestrators (Kubernetes) are out of scope for a thesis-sized deployment; the overlay is the unit of change.
