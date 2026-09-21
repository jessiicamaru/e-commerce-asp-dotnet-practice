# Tasks: An Identifier That Never Changes What It Means

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `008-immutable-release-tags`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**No unit tests.** Nothing here is unit-testable: the guarantees are properties of a pipeline and a
registry. What stands in for tests is **two negative controls** — that the check can refuse, and
that an unanswerable registry aborts rather than pushing. The second is the one whose absence would
invert the guarantee, and it is a task rather than an intention.

**No `server/` changes.** The pipeline's promises are not a property of the services.

**Organization**: by user story.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 **Check whether GHCR offers native immutable tags for this account** before writing anything. If the registry can refuse an overwrite itself, that beats a script declining to attempt one, and this feature shrinks to the retry and the reporting ([research D1](./research.md)). Record the answer either way — "we looked and it is not available" is worth as much as finding it
- [ ] T002 [P] Record the current seven digests for `main`'s latest commit, using the `digest()` helper in [quickstart.md](./quickstart.md). This is the before-picture that scenario 1 compares against, and it has to be taken **before** any change to the job
- [ ] T003 [P] Confirm the four `docker manifest inspect` outcomes still behave as [research D1](./research.md) recorded — exit 0, `manifest unknown`, `denied`, and a transport error. They were measured on 2026-09-21; the whole design rests on them and re-checking costs a minute

---

## Phase 2: Foundational

**Blocking.** Everything else depends on the decision being read correctly.

- [ ] T004 In `.github/workflows/ci.yml`, add a helper inside the publish job that classifies one image reference into `exists` / `absent` / `unanswerable`, by capturing **both** the exit code and stderr. Match `manifest unknown` and `denied` as absent; **anything else non-zero is `unanswerable`**
- [ ] T005 Make `unanswerable` **abort the release** rather than push. This is the branch that is easy to leave out and the one whose absence inverts the whole feature: a DNS failure, an expired token and an outage all exit non-zero, and reading those as "absent" overwrites a published release *because the network hiccupped* ([data-model](./data-model.md))
- [ ] T006 Add a bounded retry wrapper for `docker push` — 3 attempts, sleeping 5s then 15s, reporting each attempt and which one succeeded. Bounded on purpose: retrying forever turns a registry outage into a job nobody can read the state of ([research D3](./research.md))
- [ ] T007 Add `concurrency:` to the publish job with `cancel-in-progress: **false**`. The `false` is the load-bearing half — cancelling a publish mid-loop is exactly how a partial release happens, and this feature exists to stop producing those ([research D4](./research.md))

**Checkpoint**: the job can ask the registry and knows the difference between "no" and "could not ask".

---

## Phase 3: User Story 1 — A name, once given, keeps its meaning (P1) 🎯 MVP

**Goal**: publishing the same change twice changes nothing.

**Independent test**: [quickstart](./quickstart.md) scenario 1 — the experiment that found the defect, now producing the opposite result.

- [ ] T008 [US1] In the "Push all seven" step of `.github/workflows/ci.yml`, ask the registry about `$IMAGE_PREFIX-$service:sha-$SHORT_SHA` before pushing it, and **skip the push when it exists**. Per service, before any push for that service
- [ ] T009 [US1] Keep pushing `$IMAGE_PREFIX-$service:main` **unconditionally**, never checked. The two names exist to mean different things and FR-005 depends on the moving one still moving ([research D2](./research.md))
- [ ] T010 [US1] Report per service: `published` or `already present, left unchanged`, to both the job log and `$GITHUB_STEP_SUMMARY`. A reader who sees "skipped" everywhere needs to see that `:main` still moved
- [ ] T011 [US1] Run [quickstart](./quickstart.md) scenario 1 — publish a commit, re-run the publish job for it, compare all seven digests from the **registry**. Paste both sets. Identical is the pass
- [ ] T012 [US1] Run [quickstart](./quickstart.md) scenario 2, the control for T011: confirm `:main` **did** move after a later merge. If both names hold still the check is refusing everything and the pipeline has quietly stopped publishing — which would pass scenario 1 for entirely the wrong reason

**Checkpoint**: the permanent name means one thing. FR-001 is enforced rather than asserted.

---

## Phase 4: User Story 2 — A stopped release can be finished (P1)

**Goal**: re-running completes a partial release instead of corrupting it.

**Independent test**: [quickstart](./quickstart.md) scenarios 3 and 4.

- [ ] T013 [US2] After the push loop, ask the registry whether all seven permanent names now resolve, and report `N of 7 permanent names present`. **Ask, do not count what the loop did** — counting answers "did the steps run?", asking answers "does the release exist?", and only the second is what a release is ([data-model](./data-model.md))
- [ ] T014 [US2] Fail the job when fewer than seven resolve, naming **which** services are missing and saying that re-running will publish them and leave the rest untouched. A green job that published five is the failure mode skipping introduces
- [ ] T015 [US2] Run [quickstart](./quickstart.md) scenario 3: delete two of the seven permanent names, record the other five's digests, re-run the job. The two reappear, the five are **unchanged**. Paste the digests. This is the scenario the old behaviour got exactly backwards — re-running was both the obvious recovery and the thing that rewrote the five
- [ ] T016 [US2] Run [quickstart](./quickstart.md) scenario 4 in both directions: a complete release reports `7 of 7 … complete`, and a partial one reports `n of 7 … INCOMPLETE` naming the missing services — both readable without opening the registry
- [ ] T017 [US2] Run [quickstart](./quickstart.md) scenario 8: confirm each push reports its attempt, and that a push aimed at a name that cannot succeed makes **exactly three** attempts with the documented backoff before failing. Bounded and visible, not unbounded

**Checkpoint**: a partial release is visible, and one re-run fixes it.

---

## Phase 5: User Story 3 — The written promise matches what is enforced (P2)

**Goal**: the documents stop asserting a guarantee nothing provided.

**Independent test**: [quickstart](./quickstart.md) scenario 5, 6, 7 and the documents section.

**Do not defer T018 or T019.** They are the two negative controls, and T019 in particular — proving the release *aborts* rather than pushing when it cannot ask — is the one most likely to be skipped because everything already looks fine.

- [ ] T018 [US3] **Negative control 1 — the check can refuse.** Run the publish job twice for the same commit and read the second log: `already present, left unchanged` seven times and **no push output at all** for the permanent names. Paste it. A check never seen to skip is indistinguishable from one that always pushes, and scenario 1 would still pass on a single run, hiding it
- [ ] T019 [US3] **Negative control 2 — an unanswerable registry aborts.** Point the check at a hostname that does not resolve and confirm the release **stops**, saying it could not ask, rather than treating the failure as "absent" and pushing. Paste it
- [ ] T020 [US3] Add `workflow_dispatch` to `.github/workflows/ci.yml` with a boolean `force_republish` input defaulting to false, and update the publish job's `if:` so it runs on a push **or** a manual dispatch — while still never running on a pull request, which is what that guard was protecting against
- [ ] T021 [US3] Make `force_republish: true` skip the existence check and overwrite, reporting `republished (forced)` for each name it overwrote. It must be **unreachable by merging** — no commit-message trigger, no branch name, nothing a pull request can carry ([research D6](./research.md))
- [ ] T022 [US3] Run [quickstart](./quickstart.md) scenario 7, all three rows: a merge never overwrites; a manual run with the flag false behaves like an ordinary run; a manual run with it true overwrites and says so
- [ ] T023 [P] [US3] Correct `specs/006-release-and-rollback/quickstart.md` scenario 5 — and **record that its original expectation was falsified**, per FR-010, rather than quietly rewriting it. A corrected test looks like it always said that, and the next person loses the evidence that the scheme had a hole
- [ ] T024 [P] [US3] Correct `specs/006-release-and-rollback/contracts/release-artifacts.md`: immutability is now enforced by the pipeline, and the `denied` hole is named rather than left to be discovered
- [ ] T025 [P] [US3] Correct `CLAUDE.md` § Published images to name **what** enforces immutability, so a reader knows it is a check in a job rather than a naming convention
- [ ] T026 [US3] Correct the "Push all seven" comment in `.github/workflows/ci.yml`. It says *"a partial release is not a release"*, which is true of build→scan and says nothing about the loop that pushes — the same shape as the `fail-fast` comment corrected during feature 006, and it produced a partial release the same day it was written

**Checkpoint**: somebody reading the documents learns what actually happens.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T027 Re-read the whole publish job after all edits and confirm no credential appears and none was added. `GITHUB_TOKEN` is minted per run; `force_republish` is an input, not a secret
- [ ] T028 [P] Update `docs/README.md` if it describes what publishing guarantees
- [ ] T029 Confirm the seven extra `manifest inspect` calls did not meaningfully change the job's duration, against the numbers recorded in [007's quickstart](../007-saga-e2e-verification/quickstart.md). Seven HTTP round trips against a job that builds seven images should be invisible — say so with a number rather than assuming
- [ ] T030 Run [quickstart](./quickstart.md) scenario 1 **one final time** after every edit, because it is the guarantee the feature is named for and the one most likely to be broken by a late change

---

## Dependencies

```text
Phase 1 (T001-T003)
   └─> Phase 2 (T004-T007)          ← the decision, read correctly
          └─> Phase 3 US1 (T008-T012)   ← MVP: the name holds still
                 └─> Phase 4 US2 (T013-T017)  ← needs the skip to exist
                        └─> Phase 5 US3 (T018-T026)
                               └─> Phase 6 (T027-T030)
```

- **T001 can change everything.** If GHCR enforces immutable tags natively, T004-T005 and T008 are
  replaced by turning a setting on, and the feature becomes T006, T007, T013-T014 and the documents.
  It is first for that reason.
- **US2 depends on US1 in code**: "re-running completes a partial release" is a consequence of
  skipping, not a separate mechanism. They stay independently *testable*.
- **US3's document tasks (T023-T025) are independent** of everything and of each other; its control
  tasks are not, because falsifying the check requires the check.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T002, T003 |
| 2 | one file, sequential |
| 3 | one step, sequential; T011 and T012 each need a real run |
| 4 | T015 and T016 each need a real run, in order |
| 5 | T023, T024, T025 |
| 6 | T028 alongside anything |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 12 tasks, and it closes the defect in #12 on its own. Worth
stopping there and running scenario 1 before starting Phase 4: if the permanent name still moves,
nothing after it matters.

Then Phase 4 (recovery becomes real), Phase 5 (proof it can refuse, plus the documents), Phase 6.

**The three tasks most likely to be skipped, and why not to**: T019, because the abort branch feels
like paranoia until the day it is not and it is the one that inverts the guarantee; T012, because
`:main` moving feels obvious and is the only control that catches a check refusing *everything*; and
T023, because recording that a test was wrong is less comfortable than fixing it quietly.
