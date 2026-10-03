# Contracts: Deploy and roll back from CI

No HTTP, message or gRPC shape changes. The contracts are the script's interface and the workflow's inputs.

## `deploy.sh`

```text
deploy.sh <sha-tag | previous> [--dry-run]
```

Run from `<release dir>/server/deploy/` or anywhere. It finds the compose files beside itself (`..`).

| Environment | Default | Meaning |
| :-- | :-- | :-- |
| `ENV_FILE` | `/etc/ecommerce/.env` | the secrets (specs/141) |
| `DEPLOY_PATH` | `/opt/ecommerce` | where `releases.log` and `releases/<tag>/` live |
| `COMPOSE_PROJECT` | `ecommerce` | one project name for every release |
| `IMAGE_PREFIX` | `ghcr.io/jessiicamaru/ecommerce` | our images |
| `DEPLOY_WAIT_SECONDS` | `600` | `up --wait` timeout |
| `SMOKE_INSECURE` | unset | `1` accepts Caddy's local CA, for a laptop only |

| Exit | Meaning |
| :-- | :-- |
| 0 | deployed and healthy, or the dry run passed |
| 1 | the deploy failed; the log says whether the previous release came back |
| 2 | refused before touching anything: a bad name, a missing image or env file, or no `previous` |

Output ends with the running images (`docker compose images`), the version report the workflow puts in its summary.

## `.github/workflows/deploy.yml`

```yaml
on:
  workflow_dispatch:
    inputs:
      release: { required: true }            # sha-1a2b3c4, or previous
      dry_run: { type: boolean, default: false }
```

| Secret | What |
| :-- | :-- |
| `DEPLOY_HOST` | the server's address |
| `DEPLOY_USER` | an account in the `docker` group |
| `DEPLOY_SSH_KEY` | that account's private key, used for nothing else |
| `DEPLOY_KNOWN_HOSTS` | the server's `ssh-keyscan` line, checked by the owner |

| Variable (not secret) | Default |
| :-- | :-- |
| `DEPLOY_PATH` | `/opt/ecommerce` |

Runs in the `production` environment, so required reviewers can be added in GitHub's settings. It uses
`concurrency: deploy-production`.

## CI job `deploy-dry-run`

It runs `server/deploy/test-deploy.sh` and `.github/scripts/verify-production-overlay.sh` on every pull request and
push. No secrets and no network to a server.
