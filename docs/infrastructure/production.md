# The production stack

How the shop runs on a server, over HTTPS, from the images CI publishes (specs/141, #287). It is an **overlay** on the
two compose files every environment shares, never a copy of them, so production cannot drift from what CI and a
laptop run.

```bash
cd server
RELEASE=sha-1a2b3c4 docker compose --env-file /etc/ecommerce/.env \
  -f docker-compose.yml -f docker-compose.app.yml -f docker-compose.prod.yml up -d
```

Docker Compose **v2.24 or later** is required: the overlay removes ports and build sections with `!reset`.

## 1. What the overlay changes, and why

| Change | Why |
| :-- | :-- |
| Every service and both apps run `ghcr.io/jessiicamaru/ecommerce-<name>:${RELEASE}` | Only a `sha-` tag names a version that cannot move (specs/006, specs/008). `RELEASE` has no default, so a deploy that forgets it stops rather than running something unnamed. |
| `build: !reset null` | Nothing is compiled on the server. What runs is the bytes CI built and scanned. |
| `ports: !reset []` on every database, the broker, Seq, SeaweedFS, every service and the gateway | One door. A published database port on a server is one firewall mistake from the internet. |
| Mailpit only with the `mail-catcher` profile, pgAdmin only with `tools` | A mail catcher would swallow customers' email; a database console has no place on a server. |
| **Caddy** on 80/443, at `172.30.10.2` on the `edge` network | Terminates HTTPS and obtains and renews certificates itself. |
| Identity: `ADMIN_TOTP_SECRET: ""`, the `SMTP_*` account, `STOREFRONT_URL`, `BackOffice__Origins` | A production administrator enrols their own authenticator (specs/110); email goes to a real mail server; links and the back office's origin come from the domains. |
| The gateway: `GATEWAY_TRUSTED_PROXIES` adds Caddy, `GATEWAY_FORWARD_LIMIT: "2"` | Two proxies now stand in front of it (§3). |
| The apps: `STOREFRONT_URL` and `BACK_OFFICE_URL` as `https://` addresses | `/app-config.js` hands them to the browser for the handoff between the two apps (ADR-003). |

## 2. Two hosts, one certificate authority

ADR-003 puts the storefront and the back office on **different hosts**: cookies are scoped by host, not by port, so the
two apps on one host would share the refresh cookie and therefore a session. In production those are `SHOP_DOMAIN`
and `PORTAL_DOMAIN`, for example `shop.example.com` and `portal.shop.example.com`, both with DNS records pointing at the
server.

[deploy/Caddyfile](../../server/deploy/Caddyfile) is two site blocks, each a `reverse_proxy` to an app's nginx. With
a public domain, Caddy obtains a Let's Encrypt certificate on the first request and renews it on its own. With a name
under `.localhost`, it issues one from **its own local CA**. That is how the overlay is verified on a laptop:
`https://shop.localhost` and `https://portal.shop.localhost` are real HTTPS, and the `Secure` refresh cookie behaves
exactly as it will on the server. The certificates live in the `caddy_data` volume, so a restart does not ask Let's
Encrypt again and hit its rate limit.

## 3. Which address the sign-in limits count

The gateway limits sign-in per client address (specs/062). It believes `X-Forwarded-For` only from the proxies it is
told about. In production a request passes through two of them:

```text
browser ──► Caddy 172.30.10.2 ──► nginx 172.30.10.10 / .11 ──► gateway
            writes the browser     appends Caddy's address
```

The gateway reads the header from the right: the last entry was written by nginx (trusted), the one before by Caddy
(trusted), and the one before that is the visitor. `GATEWAY_FORWARD_LIMIT=2` lets it walk those two hops. It still
stops at the first address that is not a trusted proxy, so a visitor who sends their own `X-Forwarded-For` gains
nothing. Caddy replaces that header anyway, and the gateway would not believe it beyond the two hops.

| Setting | Default | Allowed |
| :-- | :-- | :-- |
| `GATEWAY_TRUSTED_PROXIES` | none: the header is not read at all | IPs or CIDRs, comma-separated |
| `GATEWAY_FORWARD_LIMIT` | `1`, nginx alone (development and CI) | 1 to 5; anything else stops the gateway at startup |

`AuthRateLimitTests.Two_trusted_hops_reach_the_visitor` and `Two_hops_believe_nothing_behind_an_untrusted_one` cover
both sides.

### The address Caddy needed was taken

The first local run failed: `Address already in use` for `172.30.10.2`. Docker had given that address to the gateway,
which joins `edge` with no fixed address and therefore draws from the same pool. The `edge` network now declares
`ip_range: 172.30.10.128/25`: dynamic addresses come from the upper half, and the lower half is kept for the proxies
that need fixed ones (Caddy `.2`, the storefront `.10`, the back office `.11`). An existing `server_edge` network keeps
its old settings, so remove it once (`docker network rm server_edge`, with the stack down) for the range to apply.

## 4. Email through a real mail server

Development sends to Mailpit, anonymously and in plain text. Production sends through a provider that asks for an
account over STARTTLS. Which of the two applies is configuration, not code (`SmtpEmailTransport.CreateClient`):

| Variable | Development | Production |
| :-- | :-- | :-- |
| `SMTP_HOST`, `SMTP_PORT` | `mailpit`, `1025` | the provider's host, usually `587` |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | unset | the account (an app password, not a person's password) |
| `SMTP_TLS` | `false` | `true` (STARTTLS) |
| `SMTP_FROM` | `e-commerce <no-reply@ecommerce.local>` | `Shop <no-reply@shop.example.com>`, on a domain the provider will send for |

Identity **refuses to start** when the settings could only fail later: a username without a password (or the other
way round), an empty host, a port out of range, or a sender that is not an address. A mail server that refuses every
email would otherwise be discovered by the first customer waiting for a confirmation link. `SmtpSettingsTests` cover
each case.

## 5. Secrets

They come from an env file **outside the repository**, `/etc/ecommerce/.env`, readable by root only, passed with
`--env-file`. [deploy/production.env.example](../../server/deploy/production.env.example) names every variable and
the command that generates each secret (`openssl rand ...`). Every `*.env` file is ignored by git, including a
`deploy/local.env` made for a laptop run.

| Secret | Lose it and |
| :-- | :-- |
| `JWT_SECRET` | every session ends; people sign in again |
| `TWO_FACTOR_KEY` | every enrolled authenticator stops working; staff enrol again |
| `DB_PASSWORD`, `RABBITMQ_PASSWORD`, `SEAWEEDFS_*` | the services cannot reach their data until the containers are recreated with the old ones |

## 6. Operating it

- **Seq** publishes no port. To read it, add a fourth file on the server that publishes its UI on the loopback address
  only (`services: { seq: { ports: ["127.0.0.1:5380:80"] } }`), then open an SSH tunnel from your machine
  (`ssh -L 5380:127.0.0.1:5380 <server>`) and browse `http://localhost:5380`. Nothing outside the server can reach it.
- **The first administrator** is seeded from `ADMIN_EMAIL` / `ADMIN_PASSWORD` while no administrator exists. They sign
  in to the storefront, set up two-factor sign-in, and only then reach the back office.
- **Upgrading** is the same command with a newer `RELEASE`. **Rolling back** is the same command with an earlier one.
  The expand-then-contract rule for schema changes is what makes that safe.

## 7. Verified locally

On 2026-10-03, with images tagged `local` from the compose build and `shop.localhost` / `portal.shop.localhost`:

- `docker compose ps` showed only Caddy publishing ports (80, 443, 443/udp).
- `https://shop.localhost` and `https://portal.shop.localhost` answered 200, and `/app-config.js` carried the
  `https://` addresses.
- `https://shop.localhost/api/identity/health` answered through Caddy → nginx → gateway.
- `http://` redirected to `https://` with 308.
- Every Playwright flow passed over HTTPS (8/8): sign-in with a code in the back office, crossing from the storefront
  with a handoff, a customer refused at the back office, and the storefront flows. The run used
  `E2E_IGNORE_HTTPS_ERRORS=1`, because the browser does not trust Caddy's local CA.

```bash
cd client
E2E_IGNORE_HTTPS_ERRORS=1 E2E_BASE_URL=https://shop.localhost E2E_BACK_OFFICE_URL=https://portal.shop.localhost npm run e2e
```
