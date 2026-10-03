#!/usr/bin/env bash
# Every decision deploy.sh makes, with stub docker and curl (specs/142). Seconds, no Docker, no network:
#
#   bash server/deploy/test-deploy.sh
#
# The stubs record each call and fail on demand: STUB_UNPUBLISHED (an image the registry lacks), STUB_FAIL_UP and
# STUB_FAIL_SMOKE (space-separated releases whose start or smoke checks fail), STUB_FOREIGN_IMAGE (an image of ours the
# files would run at another tag).
set -uo pipefail

SOURCE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/deploy.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
PREFIX=ghcr.io/jessiicamaru/ecommerce
A=sha-aaaaaaa B=sha-bbbbbbb C=sha-ccccccc
passed=0 failed=0

mkdir -p "$WORK/bin"
cat > "$WORK/bin/docker" <<'STUB'
#!/usr/bin/env bash
dir=""; prev=""
for a in "$@"; do [ "$prev" = -f ] && [ -z "$dir" ] && dir="$(dirname "$a")"; prev="$a"; done
case " $* " in
  *" manifest inspect "*)
    echo "manifest ${*: -1}" >> "$STUB_CALLS"
    [ -n "${STUB_UNPUBLISHED:-}" ] && [ "${*: -1}" = "$STUB_UNPUBLISHED" ] && exit 1
    exit 0 ;;
  *" config --images "*)
    for n in identity catalog orchestrator order inventory payment activity cart gateway storefront back-office; do
      if [ "${STUB_FOREIGN_IMAGE:-}" = "$n" ]; then echo "$IMAGE_PREFIX-$n:main"; else echo "$IMAGE_PREFIX-$n:$RELEASE"; fi
    done
    echo "postgres:17-alpine"; echo "caddy:2.10-alpine"; exit 0 ;;
  *" pull "*) echo "pull $RELEASE $dir" >> "$STUB_CALLS"; exit 0 ;;
  *" up "*)
    echo "up $RELEASE $dir" >> "$STUB_CALLS"; echo "$RELEASE" > "$STUB_RUNNING"
    case " ${STUB_FAIL_UP:-} " in *" $RELEASE "*) exit 1 ;; esac
    exit 0 ;;
  *" images"*) echo "images $RELEASE"; exit 0 ;;
esac
echo "unexpected docker $*" >&2; exit 99
STUB
cat > "$WORK/bin/curl" <<'STUB'
#!/usr/bin/env bash
running="$(cat "$STUB_RUNNING" 2>/dev/null)"
echo "curl ${*: -1}" >> "$STUB_CALLS"
case " ${STUB_FAIL_SMOKE:-} " in *" $running "*) echo "502 Bad Gateway" >&2; exit 22 ;; esac
echo "window.APP_CONFIG = { storefront: 'https://shop.test', backOffice: 'https://portal.test' }"
STUB
chmod +x "$WORK/bin/docker" "$WORK/bin/curl"

fresh() { # a new server: an empty DEPLOY_PATH, the env file, and the files of A, B and C shipped
  rm -rf "$WORK/srv"; mkdir -p "$WORK/srv"
  export DEPLOY_PATH="$WORK/srv/opt" ENV_FILE="$WORK/srv/ecommerce.env" STUB_CALLS="$WORK/srv/calls" STUB_RUNNING="$WORK/srv/running"
  printf 'SHOP_DOMAIN=shop.test\nPORTAL_DOMAIN=portal.test   # the back office\n' > "$ENV_FILE"
  : > "$STUB_CALLS"
  for t in $A $B $C; do
    mkdir -p "$DEPLOY_PATH/releases/$t/server/deploy"
    cp "$SOURCE" "$DEPLOY_PATH/releases/$t/server/deploy/deploy.sh"
    touch "$DEPLOY_PATH/releases/$t/server/docker-compose.yml" "$DEPLOY_PATH/releases/$t/server/docker-compose.app.yml" "$DEPLOY_PATH/releases/$t/server/docker-compose.prod.yml"
  done
  unset STUB_UNPUBLISHED STUB_FAIL_UP STUB_FAIL_SMOKE STUB_FOREIGN_IMAGE
}
# shellcheck disable=SC2034 # status and out are read by the checks, through eval
deploy() { # <release dir> args... ; sets $status and $out
  out="$(PATH="$WORK/bin:$PATH" IMAGE_PREFIX="$PREFIX" bash "$DEPLOY_PATH/releases/$1/server/deploy/deploy.sh" "${@:2}" 2>&1)"; status=$?
}
log_is() { [ "$(awk '{print $2" "$3}' "$DEPLOY_PATH/releases.log" 2>/dev/null | paste -sd, -)" = "$1" ]; }
ups() { grep '^up ' "$STUB_CALLS" | awk '{print $2"@"$3}' | sed "s#@$DEPLOY_PATH/releases/\([^/]*\)/server#@\1#" | paste -sd, -; }
check() { # <name> <condition...>
  local name="$1"; shift
  if "$@"; then passed=$((passed + 1)); echo "ok   $name"; else failed=$((failed + 1)); echo "FAIL $name"; echo "$out" | sed 's/^/     | /'; fi
}

fresh; deploy $A main
check "a moving name is refused" eval '[ $status = 2 ] && grep -q "not an immutable release" <<<"$out" && ! grep -q "^up " "$STUB_CALLS"'
fresh; deploy $A
check "no name is refused" eval '[ $status = 2 ]'
fresh; deploy $A sha-xyz
check "a malformed sha is refused" eval '[ $status = 2 ]'
fresh; rm "$ENV_FILE"; deploy $A $A
check "a missing env file is refused" eval '[ $status = 2 ] && grep -q "no env file" <<<"$out"'
fresh; export STUB_UNPUBLISHED="$PREFIX-back-office:$A"; deploy $A $A
check "an unpublished image stops it before anything changes" eval '[ $status = 2 ] && grep -q "back-office:$A is not published" <<<"$out" && ! grep -qE "^(pull|up) " "$STUB_CALLS" && [ ! -f "$DEPLOY_PATH/releases.log" ]'
fresh; export STUB_FOREIGN_IMAGE=gateway; deploy $A $A
check "files that would run another tag are refused" eval '[ $status = 2 ] && grep -q "gateway:$A is not what the files would run" <<<"$out"'

fresh; deploy $A $A
check "a release is deployed and logged" eval '[ $status = 0 ] && log_is "$A deployed" && [ "$(ups)" = "$A@$A" ] && grep -q "DEPLOYED $A" <<<"$out"'
check "every service is smoke-tested through the front door" eval '[ $(grep -c "^curl https://shop.test/api/.*/health" "$STUB_CALLS") = 8 ] && grep -q "^curl https://portal.test/$" "$STUB_CALLS"'
deploy $A previous
check "previous with one release is refused" eval '[ $status = 2 ] && grep -q "no earlier release" <<<"$out"'
deploy $B $B
check "a second release is deployed" eval '[ $status = 0 ] && log_is "$A deployed,$B deployed"'
: > "$STUB_CALLS"; deploy $B previous
check "previous runs the earlier release from its own files" eval '[ $status = 0 ] && log_is "$A deployed,$B deployed,$A deployed" && [ "$(ups)" = "$A@$A" ]'
: > "$STUB_CALLS"; deploy $A previous
check "previous after a rollback is the release rolled back from" eval '[ $status = 0 ] && [ "$(ups)" = "$B@$B" ]'

fresh; deploy $A $A; : > "$STUB_CALLS"; export STUB_FAIL_UP="$C"; deploy $C $C
check "a release that does not come up puts the previous one back" eval '[ $status = 1 ] && log_is "$A deployed,$C failed,$A rolled-back" && [ "$(ups)" = "$C@$C,$A@$A" ] && grep -q "ROLLED BACK to $A" <<<"$out"'
deploy $A previous
check "after a rollback, previous is still the release before it" eval '[ $status = 2 ]'

fresh; deploy $A $A; : > "$STUB_CALLS"; export STUB_FAIL_SMOKE="$C"; deploy $C $C
check "a failed smoke check puts the previous one back" eval '[ $status = 1 ] && log_is "$A deployed,$C failed,$A rolled-back" && [ "$(ups)" = "$C@$C,$A@$A" ]'

fresh; deploy $A $A; export STUB_FAIL_UP="$C $A"; deploy $C $C
check "a rollback that fails too says so" eval '[ $status = 1 ] && log_is "$A deployed,$C failed,$A rollback-failed" && grep -q "ROLLBACK TO $A FAILED TOO" <<<"$out"'

fresh; export STUB_FAIL_UP="$A"; deploy $A $A
check "a first release that fails has nothing to put back" eval '[ $status = 1 ] && log_is "$A failed" && [ "$(ups)" = "$A@$A" ] && grep -q "no earlier release to put back" <<<"$out"'

fresh; deploy $A $A; export STUB_FAIL_UP="$A"; : > "$STUB_CALLS"; deploy $A $A
check "redeploying the running release does not roll back onto itself" eval '[ $status = 1 ] && [ "$(ups)" = "$A@$A" ]'

fresh; deploy $A $A --dry-run
check "a dry run prints the steps and touches nothing" eval '[ $status = 0 ] && grep -q "^+ RELEASE=$A docker compose -p ecommerce" <<<"$out" && ! grep -qE "^(pull|up|manifest|curl) " "$STUB_CALLS" && [ ! -f "$DEPLOY_PATH/releases.log" ]'

echo "$passed passed, $failed failed"
[ "$failed" = 0 ]
