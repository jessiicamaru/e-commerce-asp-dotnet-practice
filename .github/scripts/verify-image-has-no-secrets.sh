#!/usr/bin/env bash
#
# Asserts that a built image carries no credential, in ANY layer.
#
#   .github/scripts/verify-image-has-no-secrets.sh <image[:tag]>
#
# Why layers and not the running container: `COPY . .` followed by `RUN rm .env`
# leaves the file fully readable in the earlier layer. `docker run --rm <image> ls
# /app` shows nothing and passes. That is the specific mistake this script exists
# to prevent, so it must never be rewritten to inspect a running container.
#
# If this fails, the credentials are already in an image. Rotate them - deleting
# the image does not recall a copy somebody pulled.
set -euo pipefail

IMAGE="${1:?usage: verify-image-has-no-secrets.sh <image[:tag]>}"

fail() {
  # ::error:: is picked up by GitHub Actions and ignored elsewhere.
  echo "::error::$1" >&2
  exit 1
}

pass() { echo "  ok  $1"; }

command -v docker > /dev/null 2>&1 || fail "docker is required"
docker image inspect "$IMAGE" > /dev/null 2>&1 || fail "no such image: $IMAGE"

echo "Inspecting every layer of $IMAGE"
echo

# 1. No .env file anywhere in any layer.
#    `docker save` streams a tar of every layer; listing it shows every path that
#    has ever existed in the image, not only what survived to the final one.
if docker save "$IMAGE" | tar -t 2> /dev/null | grep -qE '(^|/)\.env($|\.)'; then
  echo "--- offending paths ---" >&2
  docker save "$IMAGE" | tar -t 2> /dev/null | grep -E '(^|/)\.env($|\.)' >&2 || true
  fail "An .env file is present in a layer of $IMAGE. Check server/.dockerignore, and ROTATE the credentials."
fi
pass "no .env file in any layer"

# 2. No credential value baked in by a build argument or ENV instruction.
#    docker history shows the command that created each layer.
HISTORY="$(docker history --no-trunc --format '{{.CreatedBy}}' "$IMAGE")"

for name in JWT_SECRET DB_PASSWORD ADMIN_PASSWORD PGADMIN_PASSWORD RABBITMQ_PASS RABBITMQ_PASSWORD; do
  if printf '%s' "$HISTORY" | grep -qE "${name}=[^ \"']"; then
    fail "$name appears with a value in the build history of $IMAGE. Secrets belong in the environment at run time, never in a layer."
  fi
done
pass "no credential assigned in any build step"

# 3. If a real .env exists locally, check its actual values are absent too - the
#    strongest form of the check, and only possible when the file is to hand.
ENV_FILE="${ENV_FILE:-server/.env}"

if [ -f "$ENV_FILE" ]; then
  LEAKED=0
  while IFS= read -r line; do
    case "$line" in ''|'#'*) continue ;; esac
    value="${line#*=}"
    # Skip short or placeholder values: they produce false positives against
    # ordinary binary content.
    [ "${#value}" -ge 12 ] || continue
    case "$value" in your_*|*_here) continue ;; esac

    if docker save "$IMAGE" | tar -xO 2> /dev/null | grep -qaF "$value"; then
      echo "::error::A value from $ENV_FILE appears in a layer of $IMAGE (variable: ${line%%=*})" >&2
      LEAKED=1
    fi
  done < "$ENV_FILE"

  [ "$LEAKED" -eq 0 ] || fail "Real credential values found inside $IMAGE. ROTATE them."
  pass "no value from $ENV_FILE appears in any layer"
else
  echo "  SKIPPED  value check - $ENV_FILE not present (set ENV_FILE to override)"
fi

echo
echo "$IMAGE carries no credential in any layer."
