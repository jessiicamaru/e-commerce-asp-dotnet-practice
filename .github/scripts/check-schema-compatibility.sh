#!/usr/bin/env bash
#
# Reports whether a pull request changes the database in a way that earlier images
# cannot survive.
#
#   .github/scripts/check-schema-compatibility.sh <base-ref>
#
# It reads a diff, not a database, and that bounds what it may claim. It can say
# "earlier images may break"; it can never say "they will". Its job is to make a
# reviewer look, not to decide.
#
# It NEVER fails. Nothing is deployed anywhere yet, so a block would fire on a risk
# that does not exist, be overridden as a matter of course, and teach everyone that
# overriding this class of guard is routine. See
# specs/006-release-and-rollback/research.md D5.
#
# Writes a markdown report to stdout, and nothing at all when there is nothing to
# say. A check that comments on everything is noise.
set -euo pipefail

BASE_REF="${1:-origin/main}"

# Files ADDED by this pull request. Designer files and the model snapshot restate
# the migration rather than changing anything, so they would only double-count.
MIGRATIONS="$(
  git diff --name-only --diff-filter=A "${BASE_REF}...HEAD" -- '*/Migrations/*.cs' 2>/dev/null \
    | grep -v '\.Designer\.cs$' \
    | grep -v 'ModelSnapshot\.cs$' \
    || true
)"

EXAMINED=0
FINDINGS=""

# Operations that can break a reader. AlterColumn is separated on purpose: widening
# is safe and narrowing is not, and a diff cannot always tell them apart.
BREAKING='DropColumn|DropTable|RenameColumn|RenameTable'
MAYBE='AlterColumn'

while IFS= read -r file; do
  [ -n "$file" ] || continue
  [ -f "$file" ] || continue
  EXAMINED=$((EXAMINED + 1))

  while IFS= read -r op; do
    [ -n "$op" ] || continue
    FINDINGS="${FINDINGS}  • ${op} — breaking"$'\n'
  done < <(grep -oE "(${BREAKING})" "$file" | sort -u || true)

  while IFS= read -r op; do
    [ -n "$op" ] || continue
    FINDINGS="${FINDINGS}  • ${op} — may be breaking (widening is safe, narrowing is not; the diff cannot tell)"$'\n'
  done < <(grep -oE "(${MAYBE})" "$file" | sort -u || true)

  if [ -n "$FINDINGS" ]; then
    FINDINGS="\`$(basename "$file")\`"$'\n'"${FINDINGS}"
  fi
done <<< "$MIGRATIONS"

# Nothing to say. Say nothing - no comment, no annotation, no noise.
if [ -z "$FINDINGS" ]; then
  # Still report the count to stderr, so a run that examined zero files is
  # distinguishable from one that examined ten and found nothing. The last broken
  # check in this repository went unnoticed precisely because it could not tell
  # those two apart.
  echo "examined ${EXAMINED} migration file(s); nothing that strands an earlier image" >&2
  exit 0
fi

cat <<REPORT
### ⚠️ This changes the database in a way earlier images cannot survive

${FINDINGS}
An image built before this commit reads the old shape and will fail against the new
one. Redeploying a previous version would take the service down rather than restore
it — which is the opposite of what a rollback is for.

**Expand/contract** splits this into two releases:

1. **expand** — add the new shape, write both, read the new one
2. **contract** — remove the old shape, once nothing deployed reads it

See the constitution, *Technology & Implementation Constraints*.

If you are proceeding anyway, say why in the description. Nothing is deployed today,
so this may well be the right call — but it should be a decision rather than an
oversight.

<sub>Examined ${EXAMINED} migration file(s) added by this pull request. This check
reads a diff: it cannot see raw SQL in \`migrationBuilder.Sql(...)\`, and it cannot
tell a widening \`AlterColumn\` from a narrowing one. It never fails the build.</sub>
REPORT
