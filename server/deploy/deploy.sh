#!/usr/bin/env bash
# Deploys one release of the shop, or rolls back to the one before (specs/142, #288).
#
#   deploy.sh sha-1a2b3c4            run exactly that release
#   deploy.sh previous               run the release that ran before the current one
#   deploy.sh sha-1a2b3c4 --dry-run  check and print every step, touch nothing
#
# Runs ON THE SERVER, from the release's own files: $DEPLOY_PATH/releases/<tag>/server/deploy/deploy.sh. The compose
# files beside it are the ones built with that release, never main's. A deploy that does not come up healthy, or fails
# a smoke check through the front door, puts the previous release back by itself.
#
# Exit: 0 deployed (or the dry run passed), 1 the deploy failed (the log says whether the previous release came back),
#       2 refused before anything was touched.
set -uo pipefail

ENV_FILE="${ENV_FILE:-/etc/ecommerce/.env}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/ecommerce}"
PROJECT="${COMPOSE_PROJECT:-ecommerce}"
IMAGE_PREFIX="${IMAGE_PREFIX:-ghcr.io/jessiicamaru/ecommerce}"
WAIT_SECONDS="${DEPLOY_WAIT_SECONDS:-600}"
LOG="$DEPLOY_PATH/releases.log"
OURS=(identity catalog orchestrator order inventory payment activity cart gateway storefront back-office)
SERVICES=(identity catalog orchestrator order inventory payment activity cart)

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DRY=0
TARGET=""
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    *) TARGET="$arg" ;;
  esac
done

say() { printf '%s\n' "$*"; }
refuse() { say "REFUSED: $*" >&2; exit 2; }
now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
record() { if [ "$DRY" = 1 ]; then say "+ log: $1 $2"; else mkdir -p "$DEPLOY_PATH" && printf '%s %s %s\n' "$(now)" "$1" "$2" >> "$LOG"; fi; }

# The release running now, and the one that ran before it: the newest lines that ended healthy.
current_release() { [ -f "$LOG" ] && awk '$3=="deployed"||$3=="rolled-back"{t=$2} END{if(t)print t}' "$LOG"; }
previous_release() {
  [ -f "$LOG" ] || return 0
  awk '$3=="deployed"||$3=="rolled-back"{n++; tag[n]=$2}
       END{cur=tag[n]; for(i=n-1;i>=1;i--) if(tag[i]!=cur){print tag[i]; exit}}' "$LOG"
}

[ -n "$TARGET" ] || refuse "name a release: deploy.sh sha-<commit> | previous [--dry-run]"
if [ "$TARGET" = previous ]; then
  TARGET="$(previous_release)"
  [ -n "$TARGET" ] || refuse "no earlier release in $LOG to roll back to"
  say "previous is $TARGET"
  # Roll back with that release's own files, already on the server since it was deployed.
  OWN="$DEPLOY_PATH/releases/$TARGET/server"
  [ -d "$OWN" ] || refuse "the files of $TARGET are gone from $OWN"
  HERE="$OWN"
fi
[[ "$TARGET" =~ ^sha-[0-9a-f]{7,40}$ ]] || refuse "'$TARGET' is not an immutable release - only a sha- tag names something deployable (specs/008)"
[ -f "$ENV_FILE" ] || refuse "no env file at $ENV_FILE (deploy/production.env.example says what it holds)"

compose_in() { # <dir> <release> args...
  local dir="$1" release="$2"; shift 2
  RELEASE="$release" IMAGE_PREFIX="$IMAGE_PREFIX" docker compose -p "$PROJECT" --env-file "$ENV_FILE" \
    -f "$dir/docker-compose.yml" -f "$dir/docker-compose.app.yml" -f "$dir/docker-compose.prod.yml" "$@"
}

# A step that changes something: printed, not run, in a dry run.
run_compose() {
  if [ "$DRY" = 1 ]; then
    say "+ RELEASE=$2 docker compose -p $PROJECT --env-file $ENV_FILE -f $1/docker-compose{,.app,.prod}.yml ${*:3}"
  else
    compose_in "$@"
  fi
}

# What would run: the files must render, and every image of ours must carry the release - nothing else, and no :main.
images="$(compose_in "$HERE" "$TARGET" config --images 2>&1)" || refuse "the compose files do not render: $images"
for name in "${OURS[@]}"; do
  grep -qx "$IMAGE_PREFIX-$name:$TARGET" <<<"$images" || refuse "$IMAGE_PREFIX-$name:$TARGET is not what the files would run"
done
if grep "^$IMAGE_PREFIX-" <<<"$images" | grep -vq ":$TARGET\$"; then
  refuse "an image of ours would run at another tag: $(grep "^$IMAGE_PREFIX-" <<<"$images" | grep -v ":$TARGET\$" | head -1)"
fi

# Published, all of them, before anything changes - a release whose publish stopped halfway must not half-deploy.
if [ "$DRY" = 1 ]; then
  say "+ (the real run asks the registry for all ${#OURS[@]} images here)"
else
  for name in "${OURS[@]}"; do
    docker manifest inspect "$IMAGE_PREFIX-$name:$TARGET" >/dev/null 2>&1 || refuse "$IMAGE_PREFIX-$name:$TARGET is not published"
  done
fi

# One deploy at a time on this server.
if [ "$DRY" = 0 ] && command -v flock >/dev/null; then
  mkdir -p "$DEPLOY_PATH"; exec 9>"$DEPLOY_PATH/.lock"
  flock -n 9 || { say "another deploy is running; waiting for it"; flock 9; }
fi

env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/[[:space:]]*#.*$//' -e 's/^"//' -e 's/"$//'; }
SHOP="$(env_value SHOP_DOMAIN)"; PORTAL="$(env_value PORTAL_DOMAIN)"
[ -n "$SHOP" ] && [ -n "$PORTAL" ] || refuse "SHOP_DOMAIN and PORTAL_DOMAIN must be set in $ENV_FILE"

# Through the front door, as a customer would arrive: certificate, Caddy, nginx, the gateway's routes.
smoke() {
  local insecure=() failures=0
  [ "${SMOKE_INSECURE:-}" = 1 ] && insecure=(-k)
  probe() { # <url> [must contain]
    local body
    if ! body="$(curl -fsS "${insecure[@]}" --max-time 15 --retry 5 --retry-delay 3 --retry-all-errors "$1" 2>&1)"; then
      say "  FAIL $1: $body"; failures=$((failures + 1)); return
    fi
    if [ -n "${2:-}" ] && ! grep -qF "$2" <<<"$body"; then
      say "  FAIL $1: does not mention $2"; failures=$((failures + 1)); return
    fi
    say "  ok   $1"
  }
  say "smoke checks"
  probe "https://$SHOP/"
  probe "https://$PORTAL/"
  probe "https://$SHOP/app-config.js" "https://$PORTAL"
  for svc in "${SERVICES[@]}"; do probe "https://$SHOP/api/$svc/health"; done
  [ "$failures" = 0 ]
}

bring_up() { # <dir> <release>
  say "pulling $2"
  run_compose "$1" "$2" pull --quiet || return 1
  say "starting $2 (waiting up to ${WAIT_SECONDS}s for every health check)"
  run_compose "$1" "$2" up -d --wait --wait-timeout "$WAIT_SECONDS" --remove-orphans || return 1
  if [ "$DRY" = 1 ]; then say "+ smoke checks against https://$SHOP and https://$PORTAL"; else smoke; fi
}

BEFORE="$(current_release || true)"
say "deploying $TARGET (running now: ${BEFORE:-nothing recorded}) from $HERE"
if bring_up "$HERE" "$TARGET"; then
  record "$TARGET" deployed
  [ "$DRY" = 1 ] || compose_in "$HERE" "$TARGET" images
  say "DEPLOYED $TARGET"
  exit 0
fi

record "$TARGET" failed
say "DEPLOY OF $TARGET FAILED" >&2
if [ -z "$BEFORE" ] || [ "$BEFORE" = "$TARGET" ]; then
  say "no earlier release to put back - look at the stack now" >&2
  exit 1
fi
OWN="$DEPLOY_PATH/releases/$BEFORE/server"
[ -d "$OWN" ] || { say "the files of $BEFORE are gone from $OWN - cannot roll back" >&2; exit 1; }
say "putting $BEFORE back"
if bring_up "$OWN" "$BEFORE"; then
  record "$BEFORE" rolled-back
  say "ROLLED BACK to $BEFORE; $TARGET is not running" >&2
else
  record "$BEFORE" rollback-failed
  say "ROLLBACK TO $BEFORE FAILED TOO - the shop may be down; look at the stack now" >&2
fi
exit 1
