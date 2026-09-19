# Contract: The Schema Compatibility Check

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-19

What the check reads, what it says, and — the part that matters — what it refuses to claim.

---

## When it runs

On every pull request targeting `main`. Not on merges: by then the decision has been taken, and the
purpose is to inform a reviewer while they are still deciding.

## What it reads

Files **added** in this pull request under `*/Migrations/*.cs`, ignoring `*.Designer.cs` and the
model snapshot, which restate rather than change.

```bash
git diff --name-only --diff-filter=A origin/main...HEAD -- '*/Migrations/*.cs'
```

## What it looks for

| Operation | Reported as | Why |
| :--- | :--- | :--- |
| `DropColumn` | **breaking** | Old code selecting it fails on every query against that table |
| `DropTable` | **breaking** | Same, for everything reading it |
| `RenameColumn` | **breaking** | A drop wearing a friendlier word |
| `RenameTable` | **breaking** | Same |
| `AlterColumn` | **may be breaking** | Widening is safe, narrowing is not, and the diff cannot always tell |
| Everything else | nothing | Additive changes are most changes and must stay cheap |

---

## What it says

On a breaking change, a comment on the pull request:

```markdown
### ⚠️ This changes the database in a way earlier images cannot survive

`20260917070740_ReplaceProductStockQuantityWithAvailability.cs`
  • DropColumn — products.StockQuantity

An image built before this commit will select that column and fail. Redeploying
a previous version would take the service down rather than restore it.

**Expand/contract** splits this into two releases:
  1. expand   — add the new shape, write both, read the new one
  2. contract — remove the old shape, once nothing deployed reads it

Constitution → Technology & Implementation Constraints.

If you are proceeding anyway, say why in the PR description. Nothing is deployed
today, so this may well be the right call — but it should be a decision rather
than an oversight.
```

On an additive change, or no migration at all: **nothing**. No comment, no annotation, no noise.

## What it does to the pipeline

**Nothing.** It never fails, never blocks, never requires a label to override.

This was the user's choice on 2026-09-19 between three options, and the reasoning is recorded in
research D5. Briefly: nothing is deployed, so a block would fire on a risk that does not yet exist,
be overridden as a matter of course, and teach everyone that overriding this class of guard is
routine. A guard people learn to ignore is worse than no guard, because it carries authority it has
not earned.

---

## What it cannot see, stated plainly

| Cannot determine | Consequence |
| :--- | :--- |
| Whether an `AlterColumn` widens or narrows | Reported as *may be*, never as a verdict |
| Raw SQL in `migrationBuilder.Sql(...)` | A breaking change written that way is invisible to this check |
| Whether anything deployed reads the affected column | It can say *earlier images may break*; never *they will* |
| Anything about migrations that already exist | Only files **added** by this pull request |

The third row is the honest boundary. **This check exists to make a reviewer look, not to decide.**

---

## It must be negative-controlled before it is believed

A check that cannot fail is not a passing check — it is an absent one wearing a green tick.

This repository shipped exactly that five days ago: `verify-image-has-no-secrets.sh` reported clean
on an image that provably contained a secret, because it was scanning the wrong level of nesting. It
was only caught by deliberately building a leaky image and finding that the script did not notice.

So, both directions, before this check is trusted:

| Control | Must produce |
| :--- | :--- |
| A pull request adding a migration with `DropColumn` | the comment |
| A pull request adding a migration with only `AddColumn` | nothing |
| A pull request with no migration | nothing |

The check also reports **how many migration files it examined**. Examining zero and saying nothing is
indistinguishable from examining ten and finding nothing — unless it says which.

---

## Where the question is also asked

The check is automatic and therefore forgettable. The same question appears where a human is already
reading:

- **`.claude/skills/gh-pr-create/templates/pr-description.md`** — the checklist. This is the live
  path; every pull request in this repository was opened through that skill.
- **`.github/pull_request_template.md`** — created by this feature; there is none today. Covers
  anyone opening a pull request through the web interface.

> - [ ] The previous image can run against this schema, or the change is split into expand and contract
