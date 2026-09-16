---
name: gh-issue-create
description: File a GitHub issue in this repo - turn a finding into a title, a body carrying the evidence, and the right type/area/risk labels, checking for duplicates first. Use when the user asks to create, open, file or raise an issue, says "make a ticket for this", or wants something they just found written down instead of fixed now.
argument-hint: "Optional: what the issue is about, plus flags"
---

# File an issue

Repo: `jessiicamaru/e-commerce-asp-dotnet-practice`. Needs `gh` or the GitHub MCP server — if
neither answers, run `gh-setup` instead of guessing.

## Invocation

`/gh-issue-create [what it is about] [flags]` — with no flags: infer everything, show the assembled
issue, and **ask once** before creating.

| Flag | Effect |
| --- | --- |
| `--title "<text>"` | Use this title verbatim instead of deriving one. |
| `--label <label>` | Add this label (repeatable), on top of the inferred ones. |
| `--no-label` | Apply no labels at all. |
| `--assign <user>` | Assign it (repeatable). `--assign @me` for yourself. |
| `--milestone <name>` | Attach to a milestone. |
| `--relates <n>` | Reference another issue or PR (repeatable) without closing it. |
| `--dry-run` | Print title, body, labels and the exact `gh` command. Create nothing. |
| `--yes` | Skip the confirmation prompt (for non-interactive runs). |
| `--web` | Open the issue in a browser once created. |

`--dry-run` is the safe way to preview.

## Steps

### 1. Preconditions

```bash
gh auth status
```

No git state matters — an issue is not tied to a branch, and filing one never touches the working
tree. Do **not** refuse on a dirty tree the way `gh-pr-create` does.

### 2. Understand what is being filed

If the user gave the subject, use it. If they said only "file that" or "make an issue", the subject
is whatever was just being discussed — say which finding you took it to mean before writing
anything, so a wrong reading is caught in one line rather than in a finished issue.

Decide what kind of thing it is, because it drives the title prefix and the shape of the body:

| Kind | Looks like | Prefix |
| --- | --- | --- |
| **Defect** | Something behaves wrongly, or data says something untrue | `fix(<scope>):` |
| **Missing capability** | Something was never built, and its absence shows | `feat(<scope>):` |
| **Debt** | Works, but is duplicated, dead, or contradicts a decision | `refactor(<scope>):` or `chore:` |
| **Documentation** | Docs disagree with the code | `docs:` |

### 3. Gather the evidence *before* writing

This is the step that separates a useful issue from a complaint. Constitution principle V applies to
an issue as much as to a change: **a claim the reader cannot check is an assertion, not a finding.**

Run the thing that shows the problem, and keep the output:

```bash
# the query, grep or request that demonstrates it
grep -rn "IConsumer" server/src/Services/Order/
docker exec ecommerce-order-db psql -U postgres -d ecommerce_order_db -c "SELECT ..."
curl -s -o /dev/null -w '%{http_code}' http://localhost:5059/api/orders
```

If you cannot produce evidence, say so in the issue rather than dressing up a suspicion as a fact.
"I believe X, but have not reproduced it" is a perfectly good issue; "X is broken" without proof is
not.

### 4. Title

Conventional Commits with a scope, matching this repo's history — so the commit and PR that
eventually close it fall straight out of the title:

- Scope is the service or building block: `identity`, `catalog`, `order`, `orchestrator`,
  `inventory`, `payment`, `gateway`, `shared`, `contracts`, `outbox`, `infra`.
- Say the **symptom**, not the fix. `fix(order): orders never leave Submitted` beats
  `fix(order): add OrderCompletedConsumer` — the second decides the solution before anyone has
  looked.
- Imperative or declarative, no trailing period, ≤ 72 chars.

### 5. Body

```markdown
## What happens

{{The symptom, from the reader's point of view. Lead with what is untrue or missing, not with the
code.}}

## Why

{{The cause, with the evidence from step 3 pasted in — the command and its real output.}}

## What needs building

{{Numbered. Each item small enough to review. Name the constitution principles that constrain it,
because they are what a reviewer will check against.}}

## Worth deciding while building it

{{Questions the implementer will hit that this issue does not settle. Delete the section if there
genuinely are none — but there usually are, and burying them costs more than asking.}}

## Acceptance

{{Checkable outcomes, not implementation steps. Each one something someone could verify without
reading the diff.}}

## Context

{{Where this was found, and anything already known to be wrong about earlier claims. If a spec or a
doc says something this contradicts, say so here.}}
```

Delete a section rather than writing "N/A". Paste real output, not a paraphrase of it.

### 6. Labels

Same taxonomy as `gh-pr-create`, minus size:

- **type** — exactly one, from the prefix chosen in step 2.
- **area** — one or more, from the paths involved: `src/Services/Identity/` → `area: identity`;
  likewise `catalog`, `order`, `orchestrator`, `inventory`, `payment`; `src/ApiGateway/` →
  `area: gateway`; `src/BuildingBlocks/` → `area: shared`; a `DbContext` or `*/Migrations/` →
  `area: db`; `docker-compose.yml`, `.env.example`, `start-dev.*` or `.github/` → `area: infra`.
- **risk** — only when true. `risk: security` for anything touching authentication, authorization
  or secrets; `risk: breaking` when a fix would change a public endpoint, DTO or a record in
  `Ecommerce.Contracts`; `risk: migration` when it will need one.

**No `size:` label.** Size is computed from a diff, and an issue has none. Guessing one before the
work exists is a number nobody should trust.

If a label does not exist, run `gh-setup --labels` rather than creating ad-hoc labels that fragment
the taxonomy.

### 7. Duplicate check

```bash
gh issue list --state open --limit 50 --json number,title,labels
gh issue list --state closed --limit 20 --json number,title   # it may have been filed and rejected
```

Warn on a plausible match and let the user decide. A closed duplicate matters as much as an open
one: somebody may already have decided this is not worth fixing, and that decision deserves reading
before it is reopened by accident.

### 8. Create

```bash
gh issue create \
  --title "<title>" \
  --body-file <tmp>.md \
  [--label "type: fix" --label "area: order" ...] \
  [--assign <user>] \
  [--milestone <name>]
```

Write the body to a temp file in the scratchpad, not the repo. Passing it inline mangles newlines
and backticks on Windows.

### 9. Report

Give the URL, the labels applied, and anything you deliberately left out — particularly a claim you
could not produce evidence for. That is the part the reader most needs flagged.

## Guardrails

- **Do not file what you can fix in the time it takes to file it.** A one-line correction with an
  obvious fix belongs in a commit, not a ticket. Filing is for work somebody has to schedule.
- **Do not invent acceptance criteria you cannot check.** An unverifiable criterion makes the issue
  impossible to close honestly.
- **Do not decide the solution in the title.** Describe what is wrong; let whoever picks it up
  choose how.
- **One issue per finding.** Two unrelated problems in one ticket means one of them gets forgotten
  when the other is fixed.
- **Say when a claim is unverified.** An issue built on a guess that reads like a fact wastes the
  next person's afternoon.

## Related

- `gh-issues` — browse, read and pick up issues, including branching from one
- `gh-pr-create` — the PR that closes it; put `Closes #N` in its description
- `speckit-taskstoissues` — converts an existing feature's `tasks.md` into issues. Use that for a
  planned feature; use this skill for a finding that has no spec behind it
- `dev-new-session` — for work large enough to deserve a spec rather than a ticket

## Branch naming, when the issue is picked up

Including the number lets `gh-pr-create` link it automatically: `fix/2-order-status`. A feature
opened through `dev-new-session` uses `NNN-short-name` instead and links its issue by hand — the two
conventions coexist on purpose, one for tickets and one for specs.
