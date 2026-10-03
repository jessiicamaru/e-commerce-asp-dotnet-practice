#!/usr/bin/env bash
# What the production stack exposes, checked on every pull request (specs/141, specs/142) - and the deploy's dry run.
#
#   bash .github/scripts/verify-production-overlay.sh [<commit>]    # no commit: the working tree
#
# Renders the overlay from the shipped files alone (bundle-release.sh), with the env template's secrets filled by
# placeholders, and asserts:
# - only Caddy publishes a port;
# - nothing is built;
# - every image of ours runs at the release;
# - Mailpit and pgAdmin are off;
# - no TOTP secret is seeded;
# - the gateway reads two hops from known proxies.
# Then it runs deploy.sh --dry-run against the same bundle.
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
release=sha-0000000
prefix=ghcr.io/jessiicamaru/ecommerce

bash "$root/.github/scripts/bundle-release.sh" "$work/opt/releases/$release" "${1:-}"
dir="$work/opt/releases/$release/server"

# The template, with every empty value given a placeholder - what a filled-in /etc/ecommerce/.env looks like.
sed -E 's/^([A-Z_]+)=[[:space:]]*(#.*)?$/\1=placeholder-\1/' "$dir/deploy/production.env.example" > "$work/production.env"
grep -q '^SHOP_DOMAIN=shop.example.com' "$work/production.env"

RELEASE="$release" docker compose -p ecommerce --env-file "$work/production.env" \
  -f "$dir/docker-compose.yml" -f "$dir/docker-compose.app.yml" -f "$dir/docker-compose.prod.yml" \
  config --format json > "$work/config.json"

PY=""
for candidate in python3 python; do
  if "$candidate" -c 'import json' >/dev/null 2>&1; then PY="$candidate"; break; fi
done
[ -n "$PY" ] || { echo "python is needed to read the rendered config" >&2; exit 1; }

"$PY" - "$work/config.json" "$prefix" "$release" <<'PY'
import json, sys
config, prefix, release = json.load(open(sys.argv[1])), sys.argv[2], sys.argv[3]
services = config["services"]
problems = []

published = sorted(name for name, s in services.items() if s.get("ports"))
if published != ["caddy"]:
    problems.append(f"only caddy may publish ports, found: {published}")

built = sorted(name for name, s in services.items() if s.get("build"))
if built:
    problems.append(f"nothing is built in production, found build for: {built}")

ours = {name: s["image"] for name, s in services.items() if s.get("image", "").startswith(prefix + "-")}
if len(ours) != 11:
    problems.append(f"expected 11 images of ours, found {len(ours)}: {sorted(ours)}")
problems += [f"{name} runs {image}, not the release" for name, image in ours.items() if not image.endswith(":" + release)]

for tool in ("mailpit", "pgadmin"):
    if tool in services:
        problems.append(f"{tool} must stay behind its profile")

identity = services["identity"]["environment"]
if identity.get("ADMIN_TOTP_SECRET") not in ("", None):
    problems.append("ADMIN_TOTP_SECRET must be empty in production")
if not identity.get("BackOffice__Origins", "").startswith("https://"):
    problems.append("BackOffice__Origins must be the portal's https address")

gateway = services["gateway"]["environment"]
if gateway.get("GATEWAY_FORWARD_LIMIT") != "2":
    problems.append("the gateway must read two forwarded hops behind Caddy")
caddy_ip = services["caddy"]["networks"]["edge"]["ipv4_address"]
if caddy_ip not in gateway.get("GATEWAY_TRUSTED_PROXIES", "").split(","):
    problems.append(f"Caddy's {caddy_ip} is not a trusted proxy")
ip_range = config["networks"]["edge"]["ipam"]["config"][0].get("ip_range")
if ip_range != "172.30.10.128/25":
    problems.append(f"edge must keep fixed addresses out of the dynamic pool, ip_range is {ip_range}")

for p in problems:
    print("FAIL", p)
if problems:
    sys.exit(1)
print(f"ok   the overlay: {len(services)} services, only caddy published, {len(ours)} images at {release}")
PY

ENV_FILE="$work/production.env" DEPLOY_PATH="$work/opt" bash "$dir/deploy/deploy.sh" "$release" --dry-run
echo "ok   the deploy's dry run"
