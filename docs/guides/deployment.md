# Deployment: from a bare server to a running shop

A walk in order, from an empty Linux server to customers on `https://shop.example.com` and staff on
`https://portal.shop.example.com`. Each step links to the reference that explains it. The reference is the
[production stack](../infrastructure/production.md), and this guide is the path through it.

> **What has and has not been exercised.**
> - The production overlay was run in full on a laptop, over HTTPS with Caddy's own CA (2026-10-03): every browser
>   flow passed through it.
> - The deploy script's decisions are tested with stubs on every change (`test-deploy.sh`, CI's `deploy-dry-run`).
> - The SSH hop to a real server, and certificates from a public CA, run the first time the owner provides a server
>   and its secrets.
> - Payment stays the stub until a VNPay merchant account exists.

## What you need

| | |
| :-- | :-- |
| A Linux server | 4 CPUs and 8 GB of memory is comfortable: 25 containers, 8 of them PostgreSQL |
| Docker Engine with Compose **v2.24 or later** | the overlay uses `!reset` |
| Two DNS names pointing at it | `SHOP_DOMAIN` (the storefront) and `PORTAL_DOMAIN` (the back office). Two names, because staff work on an origin of their own ([ADR-003](../architecture/adr-003-storefront-and-back-office.md)) |
| Ports 80 and 443 open | the only ports anything publishes. Caddy needs 80 for the certificate challenge |
| An SMTP account | the shop sends real email: confirmations, resets, orders |
| A GitHub environment named `production` | holds the deploy secrets (step 3) |

Nothing is built on the server. It runs the images CI built, scanned and published to GHCR, named by an immutable
`sha-` tag ([specs/006](../../specs/006-release-and-rollback/), [specs/008](../../specs/008-immutable-release-tags/)).

## 1. Prepare the server

```bash
sudo adduser --disabled-password deploy && sudo usermod -aG docker deploy
sudo mkdir -p /opt/ecommerce /etc/ecommerce && sudo chown deploy: /opt/ecommerce
```

Open 80 and 443 in the firewall, and nothing else besides SSH. Every database, the broker, Seq, Prometheus and
Grafana stay unpublished in production ([production §1](../infrastructure/production.md)).

## 2. Write the secrets file

Copy [`server/deploy/production.env.example`](../../server/deploy/production.env.example) to the server as
`/etc/ecommerce/.env`, readable by root only (`install -m 600`). It names every variable and the command that
generates each secret:
- the domains;
- the database, broker, JWT, two-factor, object store, Seq and Grafana secrets;
- the first administrator's email and password;
- the SMTP account;
- the payment provider (`Stub`, or `VnPay` with a merchant code and secret).

What losing each secret costs is in [production §5](../infrastructure/production.md). Keep a copy somewhere other than
the server.

## 3. Give GitHub a way in

```bash
ssh-keygen -t ed25519 -f deploy_key -C "ecommerce deploy"      # a key for this account only
# append deploy_key.pub to /home/deploy/.ssh/authorized_keys on the server
ssh-keyscan -t ed25519 <server>                                # check the fingerprint against the server's own
```

In the repository's `production` environment, set these secrets:

| Secret | What |
| :-- | :-- |
| `DEPLOY_HOST` | the server's address |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | the private key |
| `DEPLOY_KNOWN_HOSTS` | the `ssh-keyscan` line |

The workflow pins that host key and never trusts a host on first use. Optional variables: `DEPLOY_PATH`,
`DEPLOY_ENV_FILE`, `DEPLOY_PORT` ([production §8](../infrastructure/production.md)).

## 4. Deploy a release

Every merge to `main` that passes CI publishes eleven images at `sha-<commit>`. Pick one from the
[packages](https://github.com/jessiicamaru?tab=packages) or from the merge's `publish` job, then:

```text
Actions → deploy → Run workflow
  release: sha-1a2b3c4    dry run: ticked     → checks the tag and the files, prints every step, connects to nothing
  release: sha-1a2b3c4    dry run: unticked   → deploys
```

What the deploy does, in order ([production §8](../infrastructure/production.md)):
1. It checks that all eleven images exist at that tag.
2. It ships **that commit's** compose files and `server/deploy/` (an image and the file that runs it are built
   together).
3. On the server, `deploy.sh` takes a lock, pulls, and runs `up --wait`, which waits for every container's health
   check.
4. It smoke-tests through Caddy, as a customer arrives.
5. It logs the result to `/opt/ecommerce/releases.log`.

On a failed start or a failed smoke check it **puts the previous release back by itself**, and the run still fails.

On the first deploy, Caddy obtains certificates for both names from Let's Encrypt. That needs DNS to point at the
server and port 80 open.

## 5. Sign in as the first administrator

Identity seeds the administrator from `ADMIN_EMAIL` / `ADMIN_PASSWORD` while no administrator exists.
1. Sign in on the **storefront** (`https://<SHOP_DOMAIN>/sign-in`).
2. Enrol an authenticator app at `/account/two-factor`. Production seeds no TOTP secret.
3. Use "Management platform" to cross to the back office with a single-use handoff, and enter the code there.

Staff powers exist only in a back-office session verified with a code
([specs/110](../../specs/110-staff-two-factor/), [specs/138](../../specs/138-staff-roles-back-office/),
[specs/140](../../specs/140-back-office-handoff/)).

Then, in the back office:
- grant `Moderator` to whoever moderates (`/users`);
- set delivery prices (`/delivery`);
- create categories (`/categories`).

To start with a catalogue, run `seed/seed-catalogue.py` from a workstation against the domain, with the same
administrator ([the demo script](demo-script.md)).

## 6. Check it

- `https://<SHOP_DOMAIN>` and `https://<PORTAL_DOMAIN>` load, and the browser shows a valid certificate.
- `https://<SHOP_DOMAIN>/api/<svc>/health` answers for each service (`identity`, `catalog`, `cart`, `order`,
  `inventory`, `payment`, `activity`, `orchestrator`). These are the same checks the deploy's smoke test made.
- Place an order with a test customer. With the stub, it settles to Paid within seconds, and the payment card says
  "no money is moved".

## 7. Watch it

Seq (logs and traces) and Grafana (the "E-commerce overview" dashboard) publish no port. Reach each through an SSH
tunnel, after a small fourth compose file on the server publishes it on the loopback address
([production §6](../infrastructure/production.md)):

```bash
ssh -L 5380:127.0.0.1:5380 -L 3000:127.0.0.1:3000 deploy@<server>
# then http://localhost:5380 (Seq) and http://localhost:3000 (Grafana)
```

[Observability](observability.md) covers what to look at: an order's whole trace by `OrderId` in Seq, and settle
times, outbox backlogs and queue depths in Grafana.

## 8. Upgrade and roll back

- **Upgrade**: run the deploy workflow with the newer `sha-` tag.
- **Roll back**: run it with `release: previous`. That runs, from its own files, what ran before the current release.

Without GitHub, the same script runs from a shell on the server:

```bash
/opt/ecommerce/releases/sha-1a2b3c4/server/deploy/deploy.sh sha-1a2b3c4
/opt/ecommerce/releases/sha-1a2b3c4/server/deploy/deploy.sh previous
```

Rolling back is safe because schema changes are made expand-then-contract: no migration drops, renames or narrows
what an earlier image reads. CI's `schema-compatibility` job comments on any pull request that would.

## Not covered yet

- **Backups.** The eight PostgreSQL volumes and the image bucket are not backed up by anything in this repository. A
  real deployment needs `pg_dump` per database (or volume snapshots) on a schedule, kept off the server.
- **A real payment.** VNPay's sandbox is wired and tested against the simulator ([specs/143](../../specs/143-vnpay-sandbox/)).
  Going live needs a merchant account, its IPN address registered, and `PAYMENT_PROVIDER=VnPay` with its code and
  secret.
- **One server, no redundancy.** Every service runs once. The services are stateless apart from their databases, and
  product images are in a shared bucket, so a second instance per service is a compose change. It has not been done.
