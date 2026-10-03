# Quickstart: Deploy and roll back from CI

## The decisions, with stubs (seconds)

```bash
bash server/deploy/test-deploy.sh
```

Expected: every case `ok`:
- bad names refused;
- missing images refused;
- a deploy logged;
- `previous` resolved;
- a failing `up` rolled back once.

## The overlay, as a server would receive it

```bash
bash .github/scripts/verify-production-overlay.sh
```

Expected: the overlay renders from the shipped files alone, the assertions pass, and the dry run prints its steps.

## For real, on this machine (published images, Caddy's local CA)

```bash
export ENV_FILE=<a filled env file with SHOP_DOMAIN=shop.localhost, PORTAL_DOMAIN=portal.shop.localhost>
export DEPLOY_PATH=<scratch dir> SMOKE_INSECURE=1
# copy server/docker-compose*.yml and server/deploy/ of each release into $DEPLOY_PATH/releases/<tag>/server/
$DEPLOY_PATH/releases/sha-A/server/deploy/deploy.sh sha-A
$DEPLOY_PATH/releases/sha-B/server/deploy/deploy.sh sha-B
$DEPLOY_PATH/releases/sha-B/server/deploy/deploy.sh previous     # back to sha-A
cat $DEPLOY_PATH/releases.log
```

Expected: three healthy runs. The log reads `sha-A deployed`, `sha-B deployed`, `sha-A deployed`, and
`docker compose -p ecommerce images` shows sha-A.

## From GitHub, once the secrets exist

Actions → deploy → Run workflow:
- release `sha-…` with dry run ticked: prints the plan;
- unticked: deploys;
- release `previous`: rolls back.
