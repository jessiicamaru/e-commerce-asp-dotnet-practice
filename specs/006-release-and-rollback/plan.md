# Implementation Plan: Releasable Versions, and Rollbacks That Survive

**Branch**: `006-release-and-rollback` | **Date**: 2026-09-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-release-and-rollback/spec.md`, tracked as issues
[#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8) and
[#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9)

## Summary

A merge to `main` that passes every check publishes seven images to GHCR, each tagged with the
commit it came from and never overwritten. A pull request that changes the database's shape in a way
earlier images cannot survive says so on itself, automatically, without blocking.

Four things shape the design beyond the obvious:

- **The credential scan currently runs on an image nobody will ever ship.** `image-secrets` builds
  its own Catalog image and scans that. Once images are published, the scanned artifact and the
  published artifact must be the same bytes, or FR-007 guarantees nothing (research D4).
- **There is no pull request template in this repository.** FR-011's question has nowhere to live
  yet, and the place that actually works is the `gh-pr-create` skill — every PR here was opened with
  it (research D6).
- **Amending the constitution has a procedure, and it is written in the constitution.**
  `/speckit-constitution`, a Sync Impact Report, and a MINOR version bump (research D7).
- **The constitution has already drifted.** Its Service topology paragraph says each service pins its
  HTTP port in `app.Run(...)`; feature 005 made that conditional. Found while reading for this plan
  and fixed in the same amendment, because the governance section says a disagreement is never
  resolved by ignoring it.

## Technical Context

**Language/Version**: No application code changes. The work is GitHub Actions workflow YAML, one
shell script, and Markdown

**Primary Dependencies**: GitHub Container Registry via the built-in `GITHUB_TOKEN`; `docker
buildx`; the existing `verify-image-has-no-secrets.sh`

**Storage**: GHCR packages under `jessiicamaru/`. No database change of any kind

**Testing**: Nothing unit-testable here. The guarantees are properties of a pipeline, so they are
verified by running it and reading what it produced — plus one negative control for the schema check,
because a check that cannot fail is the failure mode this repository has already shipped once
(research D5)

**Target Platform**: `ubuntu-latest` runners

**Project Type**: CI pipeline and governance. No service is touched

**Performance Goals**: Publishing seven images must not make the pipeline unusable. Cross-service
layer caching is what keeps it bounded; a naive loop rebuilds the SDK layer seven times

**Constraints**: Nothing publishes from a pull request (FR-006). Nothing publishes unless every check
passed (FR-005). A tag is never reused (FR-003). The scanned image is the published image (FR-007).
The schema check informs and does not block (FR-009)

**Scale/Scope**: One workflow job added, one modified, one script added, one PR template added, one
constitution amendment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not engaged.** No service reads another's data; no service is touched at all. Each gets its own image, which keeps the boundary visible in the artifacts as well as in the code |
| **II. Clean Architecture Layering** | **Not engaged.** No project, layer or dependency changes |
| **III. Atomic Writes and Idempotent Messaging** | **Not engaged.** No handler, no message, no write. Recorded as not engaged rather than as a pass, because a vacuous tick is what this section is for catching |
| **IV. Identity Comes From the Token** | **Not engaged** by the feature, but adjacent and worth stating: publishing makes images pullable by anyone who can read this public repository. `JWT_SECRET` in a layer would become an internet-wide problem rather than a local one, which is why FR-007 moves the scan onto the published artifact rather than leaving it on a lookalike |
| **V. Evidence Over Assumption** | **Pass, and it is the whole feature.** A published image is the artifact the checks ran against, rather than a rebuild that resembles it. The schema check is negative-controlled: a migration that drops a column must produce the comment, and one that only adds must produce nothing. "The workflow ran green" is explicitly not accepted as evidence that anything was published — the acceptance is pulling the image by its tag on a different machine |

**Constitution amendment in scope.** This feature adds a rule to Technology & Implementation
Constraints, which the versioning policy makes a **MINOR** bump: `1.0.0 → 1.1.0`. It goes through
`/speckit-constitution` with a Sync Impact Report, as the Governance section requires, and not by
hand-editing the file.

**Post-Phase 1 re-check**: no violations. Complexity Tracking is empty.

One judgement stated rather than buried: **this feature deliberately builds a capability nothing
uses.** There is nowhere to deploy, so nothing will pull these images. That is not waste — the
alternative is building the artifact story during an incident, and the schema rule in particular has
to exist *before* the migration it governs, not after. But it is the kind of decision that looks like
gold-plating six months on unless the reason is written down.

## Project Structure

### Documentation (this feature)

```text
specs/006-release-and-rollback/
├── plan.md              # This file
├── spec.md              # 3 user stories, 14 requirements, 8 success criteria
├── research.md          # Phase 0: eight decisions with rejected alternatives
├── data-model.md        # Phase 1: the tag scheme, and what makes a schema change breaking
├── quickstart.md        # Phase 1: validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   ├── release-artifacts.md     # What is published, how it is named, what is guaranteed
│   └── schema-compatibility.md  # What counts as breaking, and what the check says
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source (repository root)

```text
.github/
├── workflows/ci.yml                       # + publish job; image-secrets folded into it
├── scripts/
│   ├── verify-image-has-no-secrets.sh     # unchanged, now run on the published artifact
│   └── check-schema-compatibility.sh      # NEW — reads the migration diff, reports
└── pull_request_template.md               # NEW — there is none today

.claude/skills/gh-pr-create/
└── templates/pr-description.md            # + the schema question in the checklist

.specify/memory/constitution.md            # v1.1.0 — the expand/contract rule
docs/
├── infrastructure/running-in-containers.md # + where published images live
└── guides/getting-started.md               # + running a published image
```

**Structure Decision**: The publish job **replaces** `image-secrets` rather than sitting beside it.
Keeping both would mean one job scanning a Catalog image built for the purpose while another
publishes seven different builds — the guarantee would be about the wrong bytes. One job that builds,
scans, and only then pushes is the only shape where FR-007 means what it says.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature finishes, and what it does not

**Finishes**: a merged change leaves something you can name, fetch and run without rebuilding, and a
change that would strand earlier versions says so while somebody is still looking at it.

**Does not**: deploy any of it. There is no server, no cluster, no environment, and no rollback
command — deliberately, because acceptance criteria for those cannot be checked today. It also does
not revert the breaking change already released; that one becomes the worked example in the rule.
