#!/usr/bin/env bash
# One command per load scenario (specs/144):
#
#   server/loadtest/run.sh race|checkout|browse [k6 options, e.g. -e RATE=10]
#
# k6 runs from its own image on the compose network, against the gateway by its compose name - nothing to install, the
# same on any machine. The summary is kept in server/loadtest/results/, named after the scenario and the time, with the
# machine the containers ran on: what docker reports, which is what the stack actually had.
set -euo pipefail

scenario="${1:?usage: run.sh race|checkout|browse [k6 options]}"
shift
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -f "$here/$scenario.js" ] || { echo "no scenario '$scenario' - race, checkout or browse" >&2; exit 2; }

# The administrator, as for the verify scripts: from the environment, else server/.env.
if [ -z "${ADMIN_EMAIL:-}" ] && [ -f "$here/../.env" ]; then
  ADMIN_EMAIL="$(grep -E '^ADMIN_EMAIL=' "$here/../.env" | cut -d= -f2-)"
  ADMIN_PASSWORD="$(grep -E '^ADMIN_PASSWORD=' "$here/../.env" | cut -d= -f2-)"
  ADMIN_TOTP_SECRET="$(grep -E '^ADMIN_TOTP_SECRET=' "$here/../.env" | cut -d= -f2-)"
fi
export ADMIN_EMAIL ADMIN_PASSWORD ADMIN_TOTP_SECRET

network="${COMPOSE_NETWORK:-server_default}"
image="${K6_IMAGE:-grafana/k6:1.3.0}"
machine="$(docker info --format '{{.NCPU}} CPUs, {{.MemTotal}} bytes, {{.OperatingSystem}}, Docker {{.ServerVersion}}')"
mkdir -p "$here/results"

# Git Bash on Windows: hand docker a Windows path for the mounts, and leave the container's own paths alone.
mount="$here"
if command -v cygpath >/dev/null; then mount="$(cygpath -m "$here")"; fi

MSYS_NO_PATHCONV=1 docker run --rm --network "$network" \
  -v "$mount:/scripts:ro" -v "$mount/results:/results" \
  -e ADMIN_EMAIL -e ADMIN_PASSWORD -e ADMIN_TOTP_SECRET -e MACHINE="$machine" \
  "$image" run "$@" "/scripts/$scenario.js"
