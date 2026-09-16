---
name: dev-new-session
description: Start a new feature working session - capture the requirement, branch on the next spec number, then optionally run the Spec Kit pipeline (specify, plan, tasks) and offer to implement. Use when the user wants to start a new feature, start a new session, begin work on something new, or asks to "pick up the next thing".
argument-hint: "Optional: the requirement, plus --auto to run the whole pipeline without asking"
---

# Start a new development session

Opens a feature: requirement → branch → Spec Kit artifacts → optionally implementation.

## Invocation

```text
/dev-new-session                                      # asks for the requirement
/dev-new-session Payment service for the saga         # requirement given
/dev-new-session Payment service for the saga --auto  # run the whole pipeline, no questions
```

**Auto mode** is on when, and only when, the invocation carries the literal flag `--auto`. Once it
is on, do not ask any of the confirmation questions below — run straight through and report at the
end.

> Match the flag, never a bare word. An earlier version of this skill also triggered on `auto`
> anywhere in the text, and `/dev-new-session Làm một cái PaymentService auto success đi` matched —
> "auto" there described the payment behaviour, not the pipeline. Substring matching on a word that
> can legitimately appear in a requirement silences the user's questions by accident.

---

## Step 1 — Get the requirement

If the invocation carries a requirement, use it. Otherwise ask for it and **wait**. Do not guess,
and do not start from the backlog unless the user points you there.

One question is enough:

> What do you want to build in this session? A sentence or two on the outcome is plenty — the spec
> gets written from it.

If the answer is a single vague word ("payments", "search"), ask one follow-up for the outcome
rather than inventing scope. Everything after this step derives from it, so a wrong reading here is
expensive.

---

## Step 2 — Work out the number and the name

```bash
ls -d specs/*/ 2>/dev/null | sed 's#specs/##;s#/##'
```

- **Number**: highest existing `NNN` prefix plus one, zero-padded to three digits. No specs yet → `001`.
- **Short name**: 2–4 words from the requirement, kebab-case, action-noun where natural. Preserve
  technical terms and acronyms. `Payment service for the saga` → `payment-service`.

The branch and the spec directory **must** carry the same `NNN-short-name`, so that a year from now
`specs/004-payment-service/` and the branch that built it are obviously the same thing.

---

## Step 3 — Create the branch

Guard first — uncommitted work follows you onto a new branch and quietly becomes part of it:

```bash
git status --porcelain          # must be empty
git rev-parse --abbrev-ref HEAD # note where you are
```

If the tree is dirty, stop and tell the user what is uncommitted. Do not stash on their behalf.

Then branch from an up-to-date `main`:

```bash
git checkout main
git pull --ff-only origin main    # skip if there is no remote or it is unreachable; say so
git checkout -b <NNN-short-name>
```

If the branch already exists, stop — the number was miscalculated or the feature is already
underway. Say which, rather than picking a different name silently.

> **This skill is deliberately branch-based.** Routine work in this repository is committed straight
> to `main`; a feature opened through this skill gets its own branch because it carries a spec, a
> plan and a task list that belong together. Do not carry the branch habit back into ordinary
> changes.

---

## Step 4 — Ask how far to take it

Skip this entirely in auto mode. Otherwise ask once, offering three options:

| Option | What runs |
| :--- | :--- |
| **Full pipeline** (recommended) | `speckit-specify` → `speckit-plan` → `speckit-tasks` |
| **Stop after the plan** | `speckit-specify` → `speckit-plan` |
| **Branch only** | Nothing else; the user drives from here |

Ask it as one question with those options, not as three sequential yes/no questions.

---

## Step 5 — Run the pipeline

Pass the requirement through verbatim, and **pin the feature directory** so it matches the branch:

```text
SPECIFY_FEATURE_DIRECTORY=specs/<NNN-short-name>
```

`speckit-specify` computes its own directory otherwise, and it can land on a different number than
the branch if anything raced. Pinning it removes the possibility.

Then, in order:

1. **`speckit-specify`** — writes `spec.md` and the requirements checklist. It may stop and ask up
   to three clarification questions; those are scope decisions, so let them through **even in auto
   mode**. Auto mode suppresses *this skill's* confirmations, not genuine questions about what to
   build.
2. **`speckit-plan`** — research, data model, contracts, quickstart. Its Constitution Check runs
   against `.specify/memory/constitution.md`; if that file is still the unfilled template, say so
   plainly rather than reporting a pass that checked nothing.
3. **`speckit-tasks`** — the dependency-ordered task list.

If any step fails, stop there and report which one. Do not run the next step against a missing or
half-written artifact.

Commit the artifacts before moving on, one commit with a `docs(specs):` scope.

---

## Step 6 — Offer to implement

Skip the question in auto mode and run `speckit-implement` directly.

Otherwise report first, then ask. The report is what the user decides on, so it needs the parts
they cannot see from a file listing:

- Branch name and the feature directory
- Task count, and the MVP scope (usually just User Story 1)
- **Anything the planning turned up that the requirement did not anticipate** — a gap in an existing
  contract, an assumption that had to be made, a decision that contradicts an existing document.
  This is the most valuable thing the pipeline produces and the easiest to bury
- Anything left unverified

Then:

> Run `speckit-implement` to start building, or review the plan first?

---

## Guardrails

- **Never invent the requirement.** An empty invocation means ask, not assume.
- **Never skip the dirty-tree check.** It is the difference between a clean feature branch and one
  that silently carries unrelated work.
- **Auto mode silences confirmations, not clarifications.** A question about *what to build* still
  gets asked; a question about *whether to proceed* does not.
- **Only `--auto` turns it on.** Not the word "auto" in a sentence, not a guess from tone. When in
  doubt, ask — a question costs one message, skipping one silently decides on the user's behalf.
- **Report what surprised you.** If the plan contradicts an existing document, or a contract turned
  out to have a gap, that goes in the report at the end — not only in the file where it will be
  read later, if ever.
- **One feature per invocation.** If the requirement contains two unrelated things, say so and ask
  which one this session is for.

## Related

- `speckit-specify`, `speckit-plan`, `speckit-tasks`, `speckit-implement` — the pipeline this drives
- `speckit-constitution` — run it once if the constitution is still a template
- `speckit-analyze` — cross-checks spec, plan and tasks; worth running before implement on a large feature
- `gh-pr-create` — opens the PR when the branch is ready
