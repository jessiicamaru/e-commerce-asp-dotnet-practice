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
| **Caddy** on 80/443, at `172.30.10.2` on the `edge` network | Terminates HTTPS and obtains and renews certificates itself. It adds `Strict-Transport-Security: max-age=31536000` and drops its own `Server` header (specs/151). Every other security header - the CSP and the rest - is the apps' own nginx's, the same in every environment. |
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
- **Grafana and Prometheus** (specs/148) publish no port either. Reach Grafana the same way - publish
  `127.0.0.1:3000:3000` in that fourth file, `ssh -L 3000:127.0.0.1:3000 <server>`, browse `http://localhost:3000` -
  and sign in as `admin` with `GRAFANA_ADMIN_PASSWORD`. The dashboard ships with the release (`server/observability` is
  in the bundle), so a rollback rolls it back too. Prometheus keeps 15 days.
- **The first administrator** is seeded from `ADMIN_EMAIL` / `ADMIN_PASSWORD` while no administrator exists. They sign
  in to the storefront, set up two-factor sign-in, and only then reach the back office.
- **Upgrading and rolling back** go through `deploy.sh` and the deploy workflow (§8), which check, wait for health,
  smoke-test, and put the previous release back by themselves. The expand-then-contract rule for schema changes is
  what makes running an earlier release safe.

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

## 8. Deploying and rolling back (specs/142)

A release reaches the server one way: the **deploy** workflow, run by hand from the Actions tab with a `sha-` tag. A
merge publishes images; it never deploys them.

```text
Actions → deploy → Run workflow
  release: sha-1a2b3c4     dry run ticked (the default): check and print, connect to nothing
  release: sha-1a2b3c4     dry run unticked: deploy it
  release: previous        dry run unticked: run what ran before the current release
```

### What happens, in order

1. **The workflow** checks the release:
   - finds the commit the tag names;
   - checks that all eleven images are published;
   - takes the compose files and `server/deploy/` **from that commit**, never from `main` (an image and the file that
     runs it are built together);
   - renders and checks them (`verify-production-overlay.sh`).
2. It copies them to the server, into `$DEPLOY_PATH/releases/<tag>/`. It checks the server's host key against
   `DEPLOY_KNOWN_HOSTS` and never trusts a host on first use.
3. **On the server**, [`deploy.sh`](../../server/deploy/deploy.sh) runs from those files. It:
   - refuses a name that is not a `sha-` tag, or files that would run any image of ours at another tag;
   - asks the registry for every image before changing anything, so a release whose publish stopped halfway cannot
     half-deploy;
   - takes a lock, so two deploys never overlap;
   - pulls, then runs `docker compose up -d --wait`, which waits for every container's health check;
   - **smoke-tests through the front door**, as a customer arrives: both apps, `/app-config.js` naming the other app,
     and each service's `/api/<svc>/health` through Caddy → nginx → gateway;
   - appends the outcome to `$DEPLOY_PATH/releases.log`.
4. **If the release does not come up healthy, or a smoke check fails**, the script puts the previous release back from
   its own files. It logs `failed` and then `rolled-back`, and the run still fails. If that fails too, it logs
   `rollback-failed` and says so: someone must look now.

Every release uses the same compose project name (`ecommerce`), so each release's directory drives the same
containers and volumes. Without that, every release would start a second, empty stack.

### On the server, once

```bash
sudo mkdir -p /opt/ecommerce /etc/ecommerce
sudo chown deploy: /opt/ecommerce                    # the DEPLOY_USER, in the docker group
sudo install -m 600 production.env /etc/ecommerce/.env
ssh-keyscan -t ed25519 <server>                      # check the fingerprint, then save the line as DEPLOY_KNOWN_HOSTS
```

| Secret (environment `production`) | What |
| :-- | :-- |
| `DEPLOY_HOST`, `DEPLOY_USER` | where, and as whom |
| `DEPLOY_SSH_KEY` | a key for this account only |
| `DEPLOY_KNOWN_HOSTS` | the server's host key line |

Optional variables: `DEPLOY_PATH` (`/opt/ecommerce`), `DEPLOY_ENV_FILE` (`/etc/ecommerce/.env`), `DEPLOY_PORT` (22). A
real deploy with any secret missing stops and names it. A dry run needs none.

### Without GitHub

`deploy.sh` is the whole procedure, so an operator with a shell on the server runs it directly:

```bash
/opt/ecommerce/releases/sha-1a2b3c4/server/deploy/deploy.sh sha-1a2b3c4
/opt/ecommerce/releases/sha-1a2b3c4/server/deploy/deploy.sh previous
```

### How it is tested

- `server/deploy/test-deploy.sh` drives every decision with stub `docker` and `curl`: the refusals, `previous`, the
  log, a failing start, a failing smoke check, and a failing rollback.
- `.github/scripts/verify-production-overlay.sh` renders the stack from the shipped files alone and asserts:
  - only Caddy publishes ports;
  - nothing is built;
  - every image of ours carries the release tag;
  - the tools are off;
  - no seeded TOTP secret;
  - two forwarded hops;
  - the `edge` address range.

CI's `deploy-dry-run` job runs both on every change, and publishing waits for it. The SSH hop itself is exercised
only once the secrets exist.
