# Research: Deploy and roll back from CI

## D1. The script runs on the server; the workflow only ships and calls it

**Decision**: the deploy logic lives in `server/deploy/deploy.sh`, run on the server over SSH. The workflow resolves
the release, copies that commit's compose files and calls the script.

**Rationale**:
- The same script serves three callers: an operator with a shell on the server, the workflow, and CI's dry run.
- It can be tested without GitHub.
- The automatic rollback needs the previous release's files, which are already on the server.

**Alternatives rejected**:
- `docker context` / `DOCKER_HOST=ssh://`, driving compose from the runner. Every compose call would cross the network,
  the bind-mounted Caddyfile would be resolved on the runner, and an operator without GitHub could not deploy.
- A third-party deploy action. It is opaque, and a supply-chain dependency on the step that holds the SSH key.

## D2. The release's own compose files, not `main`'s

**Decision**: the workflow finds the commit a `sha-` tag names (`git rev-parse <short>^{commit}`) and ships
`server/docker-compose*.yml` and `server/deploy/` from that commit. Each release gets its own directory on the server,
`$DEPLOY_PATH/releases/<tag>/`.

**Rationale**: an image and the compose file that runs it are built together. Rolling back to an older image with
today's compose file could start it with settings it has never seen.

**Alternative rejected**: a single checkout of `main` on the server, updated by `git pull`. It needs git and
credentials on the server, and rolls the files forward when the images roll back.

## D3. One project name

**Decision**: every call passes `-p ${COMPOSE_PROJECT:-ecommerce}`.

**Rationale**: compose names a project after its directory, and every release has its own directory. Without a fixed
name each release would start a second stack with empty volumes, and every database would look lost.

## D4. Health by `up --wait`, then smoke checks through the front door

**Decision**: `docker compose up -d --wait --wait-timeout $DEPLOY_WAIT_SECONDS --remove-orphans`, then curl through
Caddy:
- each app's page;
- `/app-config.js` naming the other app over https;
- every service's `/api/<svc>/health`.

**Rationale**:
- `--wait` reads the health checks compose already defines.
- The smoke checks prove the path a customer uses: certificate, proxy chain and gateway routes. The container checks
  cannot see any of that.

**Alternative rejected**: polling `docker inspect` in a loop. It reimplements `--wait`.

## D5. Automatic rollback on a failed deploy

**Decision**: if `up --wait` or a smoke check fails, the script redeploys the last `deployed` release from its own
directory, smoke-tests it, logs `failed` then `rolled-back`, and exits non-zero.

**Rationale**: a failed deploy otherwise leaves a half-started stack until a person notices. The previous release is
known to have passed the same checks.

**Alternative rejected**: leave it and alert. That makes the outage last until someone reads the alert.

## D6. Registry check before anything changes

**Decision**: `docker manifest inspect` for all eleven of our images before pulling.

**Rationale**: a release whose publish stopped halfway (specs/008 describes how that happened) would otherwise pull ten
images, stop, and leave the server holding a mix of versions.

## D7. The host key is a secret, not discovered

**Decision**: `DEPLOY_KNOWN_HOSTS` holds the server's line from `ssh-keyscan`, checked by the owner once. SSH runs with
`StrictHostKeyChecking=yes`.

**Rationale**: `StrictHostKeyChecking=no` hands the deploy key's session to whoever answers on that address. The issue
named three secrets; this is the fourth, and the reason is written down.

## D8. The dry run renders, asserts and prints; it does not ask the registry

**Decision**: `deploy.sh --dry-run`:
- checks the name;
- renders the config;
- checks that every image of ours carries the tag;
- prints each command it would run.

`verify-production-overlay.sh` runs it from a directory holding only the shipped files, against a filled-in copy of the
env template. Then it asserts on the rendered JSON:
- only Caddy publishes ports;
- no `build`;
- Mailpit and pgAdmin are off;
- no seeded TOTP secret;
- two forwarded hops.

**Rationale**: on a pull request the release's images do not exist yet, so a registry check would always fail there.
The real run checks the registry first (D6).

## D9. Testing the decisions with stubs

**Decision**: `test-deploy.sh` puts fake `docker` and `curl` first on `PATH`. They record calls and fail on demand. It
asserts:
- the refusals;
- `previous` resolution;
- the log lines;
- that a failing `up` triggers exactly one rollback to the right directory.

**Rationale**: the real deploy is slow and can only fail in a few ways on demand. The stubs make every branch cheap and
repeatable in CI.
