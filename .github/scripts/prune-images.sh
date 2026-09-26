#!/usr/bin/env bash
#
# Keeps the newest KEEP release versions of each published image and deletes the older ones (GHCR).
#
# Every merge to main publishes one version per image, tagged sha-<commit> (immutable) and :main (moving) - see
# specs/006 and 008. Nothing ever removed one, so each package grew by one version per merge, forever. KEEP is
# decided with the user: 30, far more than a rollback ever reaches back.
#
# What is deleted, and nothing else:
#   - a version whose tags are ALL sha-* ...
#   - ... that is older than the KEEP newest sha-tagged versions of its package.
# Never deleted:
#   - the version tagged :main (whatever else it is tagged);
#   - a version with no tag at all - it may be a platform manifest a multi-arch index points at, and deleting
#     a child breaks a parent nobody meant to touch;
#   - anything tagged with something that is not sha-* (a hand-pushed tag means a person wanted it).
#
# Usage: prune-images.sh <owner> <keep> [--dry-run]
#   GH_TOKEN must be able to read packages (and delete them, unless --dry-run).
set -euo pipefail

OWNER="${1:?owner}"
KEEP="${2:?how many to keep}"
DRY_RUN="${3:-}"
SERVICES="identity catalog order orchestrator inventory payment cart activity gateway storefront"

total_deleted=0
for service in $SERVICES; do
  package="ecommerce-$service"

  # id, created_at and tags of every version, newest first. A missing package is not an error: it has nothing to prune.
  if ! versions=$(gh api --paginate "/users/$OWNER/packages/container/$package/versions?per_page=100" \
        --jq '.[] | [.id, .created_at, ((.metadata.container.tags // []) | join(","))] | @tsv' 2>/dev/null); then
    echo "$package: not readable (missing, or the token cannot read it) - skipped"
    continue
  fi
  versions=$(printf '%s\n' "$versions" | sort -t$'\t' -k2,2r)

  kept=0
  doomed=()
  while IFS=$'\t' read -r id created tags; do
    [ -z "$id" ] && continue
    [ -z "$tags" ] && continue                              # untagged: never ours to delete
    if printf '%s' "$tags" | tr ',' '\n' | grep -qx 'main'; then
      continue                                              # :main, whatever it is
    fi
    if printf '%s' "$tags" | tr ',' '\n' | grep -qvE '^sha-[0-9a-f]+$'; then
      continue                                              # a tag a person chose
    fi
    kept=$((kept + 1))
    [ "$kept" -le "$KEEP" ] && continue
    doomed+=("$id:$tags:$created")
  done <<< "$versions"

  echo "$package: $((kept < KEEP ? kept : KEEP)) sha- versions kept, ${#doomed[@]} older to delete"
  for entry in "${doomed[@]}"; do
    id="${entry%%:*}"
    rest="${entry#*:}"
    if [ "$DRY_RUN" = "--dry-run" ]; then
      echo "  would delete $rest"
    else
      gh api -X DELETE "/users/$OWNER/packages/container/$package/versions/$id" >/dev/null
      echo "  deleted $rest"
    fi
    total_deleted=$((total_deleted + 1))
  done
done

if [ "$DRY_RUN" = "--dry-run" ]; then
  echo "Dry run: $total_deleted version(s) would be deleted."
else
  echo "Deleted $total_deleted version(s)."
fi
