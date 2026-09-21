# Quickstart & Validation: An Identifier That Never Changes What It Means

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

Eight scenarios. **Scenario 1 is the one that matters** — it is the exact experiment that found the
defect, and it must now produce the opposite result.

This feature's guarantees live in a pipeline and a registry, so most of the evidence is a digest
read from the registry, not a green tick.

---

## Reading a digest

Every scenario below compares these. Read them from the registry, never from a job log — a log says
what was attempted, the registry says what is true:

```bash
digest() {
  docker manifest inspect --verbose "$1" 2>/dev/null \
    | python -c "import sys,json;d=json.load(sys.stdin);print((d[0] if isinstance(d,list) else d)['Descriptor']['digest'])"
}

SHA=$(git rev-parse --short origin/main)
for s in identity catalog order orchestrator inventory payment gateway; do
  printf "%-14s %s\n" "$s" "$(digest ghcr.io/jessiicamaru/ecommerce-$s:sha-$SHA)"
done
```

---

## Scenario 1 — Publishing the same change twice changes nothing *(US1, FR-001, SC-001)*

**This is 006's quickstart scenario 5, which predicted the wrong answer.** It is repeated here
because a test that was silently corrected looks like it always said that.

1. Record all seven digests for the current `main`.
2. Re-run the publish job for that same commit.
3. Record them again.

**Expect**: seven identical digests, and a log saying `already present, left unchanged` seven times,
closing with `7 of 7 permanent names present`.

**Before this feature the answer was**: three digests changed within the hour —
`b7bfe138 → 8d98ec97`, `25a9242b → 02deb77c`, `8196d2b6 → 5bca4688`.

**Failure looks like**: any digest differing. The guarantee is gone and the name is decorative
again.

---

## Scenario 2 — The moving name still moves *(FR-005, SC-005)*

Record `:main`'s digest. Merge something else. Read it again.

**Expect**: it **changed**.

**This is the control for scenario 1.** If both names hold still, the check is refusing everything
and the pipeline has stopped publishing — which would pass scenario 1 for entirely the wrong
reason. One name must hold and the other must move.

---

## Scenario 3 — A partial release is completed by re-running it *(US2, FR-004, SC-002)*

Reproduce a partial release, then recover from it.

1. Publish a change, then **delete** two of the seven permanent names from the registry (this stands
   in for a push that never happened, which is hard to cause on demand).
2. Record the digests of the five that remain.
3. Re-run the publish job for that same commit.

**Expect**: the two missing names appear, **the five recorded digests are unchanged**, and the log
reads `published` twice and `already present, left unchanged` five times.

**This is the scenario the old behaviour got exactly backwards**: re-running was the obvious
recovery and was also what rewrote the five.

---

## Scenario 4 — A release says whether it is whole *(FR-007, SC-003)*

Read a run's summary without opening the registry.

| Situation | Expect |
| :-- | :-- |
| everything published | `7 of 7 permanent names present — release complete` |
| something missing | `n of 7 … INCOMPLETE`, **naming which services**, and how to finish it |

**Failure looks like**: a green job that published five. Once skipping is normal, "nothing to do"
and "nothing done" are indistinguishable unless the release says which — which is why the count is
asked of the registry rather than counted from the loop.

---

## Scenario 5 — NEGATIVE CONTROL: the check can refuse *(FR-002, FR-003)*

**Do not skip this.** A check that has never been seen to skip is indistinguishable from one that
always pushes — and this repository has shipped a check that could not fail and believed it for five
days.

Run the publish job twice for the same commit and read the **second** log.

**Expect**: `already present, left unchanged` for all seven, and **no push output at all** for the
permanent names.

**Failure looks like**: push output on the second run. The check ran and decided to push anyway,
which means it is reading its input wrongly — and scenario 1 would still pass on a single run,
hiding it.

---

## Scenario 6 — NEGATIVE CONTROL: an unanswerable registry aborts, it does not push *(FR-002)*

The branch that is easiest to leave out, and the one whose absence inverts the whole guarantee.

Make the registry unreachable — point the check at a hostname that does not resolve — and run it.

**Expect**: the release **aborts**, saying it could not ask the registry. It must **not** treat the
failure as "the name is absent" and push.

**Why this is worth its own scenario**: `exit ≠ 0` reads naturally as "not there, go ahead". A DNS
failure, an expired token and an outage all exit non-zero. Getting this wrong means a network hiccup
overwrites a published release, using the check built to prevent that
([research D1](./research.md)).

---

## Scenario 7 — Deliberate replacement works, and only deliberately *(FR-008)*

| Attempt | Expect |
| :-- | :-- |
| merge anything at all | never overwrites a permanent name, whatever the commit message says |
| run the workflow manually with `force_republish` **false** | skips, same as an ordinary run |
| run it manually with `force_republish` **true** | overwrites, and the log says `republished (forced)` |

**Failure looks like**: a path to overwriting that a merge can reach. The escape hatch exists so
nobody deletes a package instead; it stops being an escape hatch the moment it can be taken by
accident.

---

## Scenario 8 — A transient registry fault no longer halves a release *(FR-006, SC-004)*

Hard to cause on demand — `unknown blob` appeared once and did not recur on the next merge. Verify
the mechanism rather than waiting for the fault:

- Read the job log of a normal run and confirm each push reports its attempt.
- Point one push at a name that will fail, and confirm **three** attempts with the documented
  backoff, then a failure — not an unbounded retry.

**Expect**: bounded, visible, and it gives up rather than waiting out an outage.

---

## Documents that must change with the code *(US3, FR-009, FR-010, FR-011)*

Confirm each of these reads true afterwards:

| Document | Was |
| :-- | :-- |
| [006 quickstart](../006-release-and-rollback/quickstart.md) scenario 5 | "Expect: the same digest" — falsified. Must state the correct expectation **and record that it was wrong**, per FR-010 |
| [006 release-artifacts](../006-release-and-rollback/contracts/release-artifacts.md) | immutability as a property; now enforced, with the `denied` hole named |
| `CLAUDE.md` § Published images | "immutable; the only deployable name" — must name what enforces it |
| `ci.yml` "Push all seven" comment | "a partial release is not a release" — must say which part of the job that is true of |

**SC-006 is a reading test, not a code change**: someone who has not seen this incident should be
able to say, from the documents alone, what happens when the same change is published twice — and
be right.

---

## What passing all eight does not prove

- **That anything is deployed.** Nothing is. This makes a version safe to return to; returning to it
  is still a person's act.
- **That the artifact behind a name is good.** If the name exists, it passed what was required when
  it was published. Re-verification is a different feature ([research D5](./research.md)).
- **That builds are reproducible.** They are not, deliberately. The guarantee comes from refusing to
  overwrite, not from producing identical bytes.
- **That `denied` can never mask an existing package.** It can, and the contract says so. That is
  the one hole left in FR-001.
