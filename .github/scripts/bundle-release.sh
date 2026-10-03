#!/usr/bin/env bash
# The files a server receives for one release (specs/142): the compose files and server/deploy/, exactly as they were
# at the release's commit - never main's, because an image and the file that runs it are built together.
#
#   bundle-release.sh <out-dir> [<commit>]     # no commit: the working tree, for checking local changes
#
# Writes <out-dir>/server/{docker-compose.yml,docker-compose.app.yml,docker-compose.prod.yml,deploy/}.
set -euo pipefail

out="${1:?usage: bundle-release.sh <out-dir> [<commit>]}"
commit="${2:-}"
root="$(git rev-parse --show-toplevel)"
files=(server/docker-compose.yml server/docker-compose.app.yml server/docker-compose.prod.yml server/deploy)

mkdir -p "$out"
if [ -n "$commit" ]; then
  git -C "$root" archive "$commit" "${files[@]}" | tar -x -C "$out"
else
  for f in "${files[@]}"; do mkdir -p "$out/$(dirname "$f")"; cp -R "$root/$f" "$out/$(dirname "$f")/"; done
fi
chmod +x "$out/server/deploy/deploy.sh"
echo "bundled ${commit:-the working tree} into $out/server"
