# Tasks: Releasable Versions, and Rollbacks That Survive

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `006-release-and-rollback`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**No test project.** Nothing here is unit-testable — the guarantees are properties of a pipeline.
They are verified by running it and reading what it produced, plus **negative controls** on both new
checks. That is not optional: this repository shipped a check that could not fail five days ago.

**Organization**: by user story.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 Confirm GHCR publishing is available for this account and that the repository's Actions settings allow `packages: write`. If a package cannot be created, stop here and say so — everything in US1 depends on it, and discovering it at the push step wastes a full pipeline run
- [ ] T002 [P] Record the current pipeline duration from a recent run of `.github/workflows/ci.yml`, so T014's "did publishing make this unusable" has something to compare against

---

## Phase 2: Foundational

- [ ] T003 Add `permissions:` to `.github/workflows/ci.yml` — `contents: read` at the workflow level, and `packages: write` on the publish job only. Least privilege per job, not one blanket grant at the top
- [ ] T004 Confirm the event guard expression before writing the job that depends on it: the workflow triggers on **both** `push` to `main` and `pull_request` to `main`, so `if: github.event_name == 'push'` is what separates a merge from a proposal (research D3)

---

## Phase 3: User Story 1 — Every accepted change leaves something addressable (P1) 🎯 MVP

**Goal**: a merge to `main` publishes seven immutably-tagged images; nothing else does.

**Independent test**: quickstart scenarios 1–6.

- [ ] T005 [US1] Replace the `image-secrets` job in `.github/workflows/ci.yml` with a `publish` job that builds all seven services from `server/Dockerfile`, each with its own `PROJECT` build argument. **Replace, do not add alongside** — two jobs would mean one scans a Catalog image built for the purpose while the other publishes seven different builds, and FR-007 would be a statement about bytes nobody receives (research D4)
- [ ] T006 [US1] Order the steps **build → scan → push**, per service, and fail the job on the first scan failure. Pushing before scanning detects a leaked credential after the damage; this repository is public and a pulled credential must be rotated, not deleted
- [ ] T007 [US1] Tag each image `ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>` plus a moving `:main`. **No `latest`** — a moving tag makes "the previous version" a question with no answer, which is exactly what FR-004 forbids
- [ ] T008 [US1] Gate the job on `needs: [build, auth-smoke]` **and** `if: github.event_name == 'push'`. Both, not either: without the first a merge with failing tests publishes something that looks releasable; without the second every pull request publishes
- [ ] T009 [US1] Run quickstart scenario 1 after merging — seven images present for that commit, **0 rebuilds**. Paste the real output
- [ ] T010 [US1] Run quickstart scenario 2: `docker pull` and run one published image and get a healthy service. **A green pipeline is not evidence of this** (constitution V) — a published image that cannot be pulled and run is a build artifact with a URL
- [ ] T011 [US1] Run quickstart scenario 3: open a pull request, confirm **nothing** is published for its head commit
- [ ] T012 [US1] Run quickstart scenario 5: record an image digest, re-run the publish job for the same commit, confirm the digest is unchanged. Then confirm `:main` **does** move after another merge — that contrast is what the contract rests on
- [ ] T013 [US1] Run quickstart scenario 6: read the job log and confirm build → scan → push, seven times, in that order. Confirm each scan reports `read N filesystem layer(s)` with N > 0
- [ ] T014 [US1] Compare the pipeline duration against T002 and record both numbers. If publishing has made it painful, say so rather than leaving the next person to discover it

**Checkpoint**: a merge produces artifacts you can name, fetch and run. Issue #8 is closed.

---

## Phase 4: User Story 2 — A stranding schema change is visible at review (P1)

**Goal**: a pull request that removes or narrows part of the schema says so, on itself, automatically.

**Independent test**: quickstart scenario 7, in **both** directions.

- [ ] T015 [US2] Create `.github/scripts/check-schema-compatibility.sh` — read files **added** under `*/Migrations/*.cs` in this pull request, excluding `*.Designer.cs` and the model snapshot, and match `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, `AlterColumn` (contracts/schema-compatibility.md)
- [ ] T016 [US2] Report `AlterColumn` as **may be breaking**, never as a verdict. Widening is safe and narrowing is not, and the diff cannot tell them apart. A check that overstates is a check that gets ignored
- [ ] T017 [US2] Have the script state **how many migration files it examined**. Examining zero and saying nothing is indistinguishable from examining ten and finding nothing — and that ambiguity is how the last broken check went unnoticed
- [ ] T018 [US2] `git update-index --chmod=+x .github/scripts/check-schema-compatibility.sh`. **`chmod` alone will not do it** — this repository has `core.filemode=false`, and the last script added here failed CI with `Permission denied` for exactly this reason
- [ ] T019 [US2] Add a `schema-compatibility` job to `.github/workflows/ci.yml`, running on pull requests only, with `pull-requests: write` so it can comment. It **must never fail the pipeline** (FR-009)
- [ ] T020 [US2] **Negative control, positive direction**: open a pull request adding a migration with `DropColumn` and confirm the comment appears, names the file and column, and points at expand/contract. Paste it
- [ ] T021 [US2] **Negative control, negative direction**: open one adding a migration with only `AddColumn`, and one with no migration at all. Confirm **nothing** is posted in either case. A check that comments on everything is noise; a check that comments on nothing is absent
- [ ] T022 [US2] Confirm the job is green in every one of the three cases above. It informs; it does not block

**Checkpoint**: the trap that took Catalog down on rollback now announces itself while somebody is still deciding.

---

## Phase 5: User Story 3 — The rule is written where it binds (P2)

**Goal**: somebody planning a schema change can find the rule before writing it.

**Independent test**: a person unfamiliar with this feature can classify a given change from the written rule alone.

- [ ] T023 [US3] Amend the constitution **through `/speckit-constitution`, not by hand**. The constitution states its own amendment procedure and this plan is bound by it: a Sync Impact Report at the top, and dependent templates, skills and runtime guidance reviewed in the same change
- [ ] T024 [US3] Add the expand/contract rule to **Technology & Implementation Constraints**, beside the existing Persistence paragraph — a constraint on how a change is shaped, not a sixth Core Principle. State what counts as breaking, the two-release shape, and the rejected alternative, as Governance requires of a recorded decision
- [ ] T025 [US3] Bump the version `1.0.0 → 1.1.0`. **MINOR**: the policy says an added section is MINOR
- [ ] T026 [US3] In the same amendment, correct the **Service topology** paragraph, which says each service "pins its own HTTP port in `app.Run(...)`". Feature 005 made that conditional on `ASPNETCORE_URLS`. Governance says a disagreement between the code and this document is never resolved by ignoring it — and this one was found by reading for a different purpose entirely
- [ ] T027 [US3] Acknowledge the already-released breaking change (`products.StockQuantity`, PR #5) in the rule as its worked example (FR-014). A real example is worth more than an invented one, and leaving it unmentioned makes the rule look like it has never been tested
- [ ] T028 [P] [US3] Create `.github/pull_request_template.md`. **There is none today** — verified. Include the schema question
- [ ] T029 [P] [US3] Add the same question to `.claude/skills/gh-pr-create/templates/pr-description.md`. **This is the live path**: every pull request in this repository was opened through that skill, so the GitHub template alone would put the question where nobody currently looks

**Checkpoint**: the rule exists, is findable, and is asked at review time by two independent routes.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T030 [P] Update `docs/infrastructure/running-in-containers.md` — where published images live, and how to run one
- [ ] T031 [P] Update `docs/guides/getting-started.md` with a third way to run: pull a published image rather than build
- [ ] T032 [P] Update `docs/README.md` and `CLAUDE.md` — the new CI jobs, the tag scheme, and the expand/contract rule
- [ ] T033 Re-read the whole of `.github/workflows/ci.yml` after all edits and confirm no credential appears in it, per the constitution's Configuration rule. `GITHUB_TOKEN` is run-scoped and is not a stored secret, which is the point of choosing GHCR

---

## Dependencies

```text
Phase 1 (T001-T002)
   └─> Phase 2 (T003-T004)
          ├─> Phase 3 US1 (T005-T014)   ← MVP: images exist and are addressable
          └─> Phase 4 US2 (T015-T022)   ← independent of US1 in code
                 └─> Phase 5 US3 (T023-T029)
                        └─> Phase 6 (T030-T033)
```

- **US1 and US2 share no file** beyond `ci.yml` and can be built in either order. US1 is the MVP
  because #8 is the half with nothing at all today.
- **US3 depends on US2** only in sequencing: the check's comment points at the rule, so the rule
  should exist by the time anyone follows the link.
- **T023–T027 are one amendment**, not five. They are split by concern so nothing is forgotten, not
  because they are five edits.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T001, T002 |
| 3 | T005–T008 are one job definition — write it once, then T009–T014 in order |
| 4 | T015–T017 are one script; T020–T022 must be sequential (each needs a pull request) |
| 5 | T028, T029 |
| 6 | T030, T031, T032 |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 14 tasks, and it closes #8 on its own. Worth stopping there
and running quickstart scenario 2 before starting Phase 4: a published image that cannot be pulled
and run means the job needs rework, and finding that out after building the schema check is finding
it out late.

Then Phase 4 (#9's mechanism), then Phase 5 (#9's rule), then the docs.

**Do not defer T020, T021 or T013.** They are the three tasks that distinguish a working check from a
green tick. T021 in particular — proving the check stays **silent** when it should — is the one most
likely to be skipped, and it is the direction that fails silently.
