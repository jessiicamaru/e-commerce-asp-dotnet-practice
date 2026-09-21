# Implementation Plan: An Identifier That Never Changes What It Means

**Branch**: `008-immutable-release-tags` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-immutable-release-tags/spec.md`

## Summary

Feature 006 promised that `sha-<short-sha>` would never change what it points at. It changed within
the hour: a publish job failed partway through, was re-run on the same commit, and three already-
published images came back as different artifacts under the same name. Immutability was a property
of the naming convention and never of the pipeline.

This makes the pipeline enforce it. Before pushing a permanent name the release asks the registry
whether it already resolves, and skips it if so — which simultaneously makes re-running the
*correct* way to finish a partial release rather than the way to corrupt one. Each push is retried
three times so a transient `unknown blob` stops producing half-releases, publishing is serialized so
two runs cannot interleave, and the run closes by asking the registry whether all seven names exist
rather than counting what its own loop did.

The decision is made on the registry's **error text**, not its exit code. `exit ≠ 0` means "tag
absent", "package absent" **or** "could not ask" — and reading the third as the first would
overwrite a release because the network hiccupped, using the check built to prevent it. The four
cases were measured ([research D1](./research.md)).

## Technical Context

**Language/Version**: Bash inside GitHub Actions. No application code changes.

**Primary Dependencies**: `docker manifest inspect`, `docker push`, `python3` for digest extraction.
Nothing new is added.

**Storage**: None. The registry is the source of truth about what exists; the pipeline keeps no
record of its own, because a second record is a second thing that can disagree.

**Testing**: Eight quickstart scenarios, **two of them negative controls** — that the check can
refuse, and that an unanswerable registry aborts rather than pushing. The second is the one whose
absence would invert the guarantee.

**Target Platform**: `ubuntu-latest` runners; GHCR.

**Project Type**: A change to one job in `.github/workflows/ci.yml`, plus four documents that
currently assert something untrue.

**Performance Goals**: Seven extra `manifest inspect` calls per release, each a single HTTP round
trip. Immaterial against a job that builds seven images.

**Constraints**:

- Builds are not reproducible, so the guarantee must come from *not overwriting*, never from
  producing identical bytes.
- `denied` cannot distinguish "no such package" from "no permission to read". Named in the contract
  as the one hole left in FR-001.
- The escape hatch must be unreachable by merging.

**Scale/Scope**: One job rewritten, one workflow trigger added, four documents corrected. No
`server/` change.

## Constitution Check

*GATE: evaluated against [constitution v1.1.0](../../.specify/memory/constitution.md).*

| Principle | Verdict | How this design satisfies it |
| :-- | :-- | :-- |
| **I. Service Autonomy** | **PASS, not applicable** | Nothing touches a service, its data or another service's data. The seven images are treated as seven independent artifacts that happen to share a change identifier — six published and one missing is a partial release, not a broken sixth of one. |
| **II. Clean Architecture Layering** | **PASS, not applicable** | No .NET code. Stated rather than ticked silently. |
| **III. Atomic Writes and Idempotent Messaging (NON-NEGOTIABLE)** | **PASS — and it is the same idea one level up** | No database and no messages, so the letter does not apply. The spirit is the whole feature: *"State transitions MUST be written so that a repeated attempt affects zero rows rather than applying the effect a second time."* Replace "rows" with "published artifacts" and that is the defect being fixed — a repeated publish applied its effect a second time. The fix has the same shape as `TrySettleAsync`'s guarded `UPDATE`: check the current state, act only if it is what you expect. |
| **IV. Identity Comes From the Token** | **PASS** | Authentication is `GITHUB_TOKEN`, minted per run and scoped by the job's `permissions` block. No stored credential, and none added. The `force_republish` escape hatch is gated by who can run a workflow, which is GitHub's own authorization, not a flag the pipeline invents. |
| **V. Evidence Over Assumption** | **PASS — the feature exists because of it** | The defect was found by *running* quickstart scenario 5 rather than trusting the contract. Every load-bearing mechanic here was measured against the real registry before being designed on: the four `manifest inspect` outcomes, their exact stderr, and that `unknown blob` did not recur on the next merge. Where something could not be established — whether GHCR offers native immutable tags — the plan says to check rather than assuming either way. |

**Development Workflow gates**:

- *"A check that cannot express the property it is meant to guard MUST be replaced, not weakened."*
  The contract asserted immutability while nothing enforced it, which is the same defect in prose.
- *"New behaviour that cannot be verified by hand requires an automated check."* The negative
  controls are quickstart scenarios rather than tasks somebody might get to.

**No Complexity Tracking entries.** Nothing here bends a principle.

**Post-Phase-1 re-check**: unchanged. The design added a contract and a decision table; it
introduced no service dependency, no message, no stored state.

## Project Structure

### Documentation (this feature)

```text
specs/008-immutable-release-tags/
├── plan.md                        # this file
├── spec.md                        # what and why
├── research.md                    # 7 decisions, measured
├── data-model.md                  # the decision table and release states
├── quickstart.md                  # 8 scenarios, 2 of them negative controls
├── contracts/
│   └── publish-behaviour.md       # guarantees, non-guarantees, and the known hole
├── checklists/
│   └── requirements.md
└── tasks.md                       # produced by /speckit-tasks
```

### Source (repository root)

```text
.github/workflows/ci.yml           # `publish` job: check, retry, serialize, verify
                                   # plus `workflow_dispatch` with force_republish

CLAUDE.md                          # § Published images - name what enforces immutability

specs/006-release-and-rollback/
├── quickstart.md                  # scenario 5 - corrected, AND recorded as falsified
└── contracts/release-artifacts.md # immutability: property -> enforced, hole named
```

**Structure Decision**: the logic stays **inline in the job**, not extracted to
`.github/scripts/`. The three existing scripts there are each used from more than one place or
runnable locally against a developer's machine; this is a dozen lines that only ever run inside one
job, against a registry a developer has no reason to push to. Extracting it would add a file to
keep in sync for no second caller. If a second caller appears, extract it then.

**Nothing under `server/` changes.** The pipeline's guarantees are not a property of the services.

### The job, after

```text
build ──┬──► auth-smoke  ──┐
        └──► saga-e2e    ──┼──► publish   (push only, serialized)
                           │      │
        image-secrets ─────┘      ├─ build + scan all seven         (unchanged)
        schema-compatibility      ├─ per service: ask, then push or skip
                                  ├─ push :main always, retried
                                  └─ ask the registry: 7 of 7?
```

`workflow_dispatch` is added as a third trigger so the escape hatch exists. The publish job's guard
becomes *"push, or a manual run"* rather than *"push"* alone — and its `if:` must still exclude
pull requests, which is what it was protecting against in the first place.

## Complexity Tracking

> No Constitution Check violations. This table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
