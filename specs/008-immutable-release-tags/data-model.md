# Data Model: An Identifier That Never Changes What It Means

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

No schema, no tables, no messages. What this feature has instead is **one decision, taken seven
times**, and the states a release can be in. Getting the decision table wrong is how a guarantee
becomes a comment, so this document is that table.

---

## The two names

| Name | Shape | Promise | Checked before pushing? |
| :-- | :-- | :-- | :-- |
| **permanent** | `ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>` | refers to one artifact, forever | **yes** — pushed only if absent |
| **moving** | `ghcr.io/jessiicamaru/ecommerce-<service>:main` | refers to the most recent release | no — pushed every time |

The asymmetry is the whole design. One name answers *"which version is this?"* and must never
change its answer. The other answers *"what is current?"* and is useless unless it does.

---

## The decision, per service

Taken once for each of the seven, before anything is pushed under the permanent name.

```text
                    docker manifest inspect <permanent name>
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
     exit 0                  exit≠0, stderr says          exit≠0, anything
        │                  "manifest unknown"                  else
        │                    or "denied"                        │
        ▼                           ▼                           ▼
 ┌─────────────┐            ┌─────────────┐            ┌─────────────────┐
 │    SKIP     │            │    PUSH     │            │      ABORT      │
 │ leave it    │            │ (retryable) │            │ the registry    │
 │ unchanged   │            │             │            │ could not be    │
 └─────────────┘            └─────────────┘            │ asked - do not  │
        │                           │                  │ guess           │
        └───────────┬───────────────┘                  └─────────────────┘
                    ▼
          push the MOVING name
            (always, retryable)
```

**The third branch is the one that is easy to leave out**, and leaving it out is worse than having
no check. `exit ≠ 0` reads naturally as "not there, go ahead" — and a DNS failure, an expired token
or a registry outage all produce `exit ≠ 0`. Treating those as *absent* would overwrite a published
release because the network hiccupped, using the very check meant to prevent it.

The three messages were measured, not assumed ([research D1](./research.md)).

---

## What a service ends a release in

| Outcome | Meaning | Release is still whole? |
| :-- | :-- | :-- |
| `published` | the name did not exist; it does now | yes |
| `already present, left unchanged` | the name existed; nothing was touched | yes |
| `republished (forced)` | the name existed and was deliberately overwritten | yes, and the record says so loudly |
| `failed` | push failed after every retry | **no** |
| `aborted` | the registry could not be asked | **no**, and nothing further is attempted |

`published` and `already present` are **both success**. That is the point of the feature and also
its main risk: once skipping is normal, a run that skipped everything and a run that did nothing at
all look alike from the inside. Which is why the release does not decide its own completeness —

---

## Completeness is asked, not counted

At the end of a release, the seven permanent names are looked up again. The release is **complete**
when all seven exist, whoever put them there and whenever.

```text
complete   := every one of the seven permanent names resolves
partial    := at least one does not
```

Counting what the loop pushed would answer *"did the steps run?"*. Asking the registry answers
*"does the release exist?"*, which is the only question anybody has at the moment it matters.

This also makes the recovery story true rather than hoped for: a partial release plus one re-run is
a complete release, because the re-run pushes exactly the ones that are missing and leaves the rest
alone.

---

## The states a release moves through

```text
     ┌──────────────────────────────────────────┐
     │              not published               │
     └───────────────────┬──────────────────────┘
                         │ a release runs
            ┌────────────┴────────────┐
            ▼                         ▼
     ┌─────────────┐          ┌──────────────┐
     │  COMPLETE   │          │   PARTIAL    │ ◄── the state feature 006 could
     │  7 of 7     │          │  n of 7      │     produce and could not name
     └──────┬──────┘          └──────┬───────┘
            │                        │ re-run
            │ re-run                 │  (pushes the missing,
            │  (skips all seven,     │   skips the present)
            │   changes nothing)     ▼
            │                 ┌─────────────┐
            └────────────────►│  COMPLETE   │
                              └─────────────┘
```

**Before this feature the arrow from PARTIAL led back to a different COMPLETE** — one where the
three already-published names now referred to different bytes. The recovery was also the corruption,
which is the worst possible shape for a recovery procedure, because it is the thing anybody would
try first.

---

## What is deliberately not modelled

- **Whether what a name refers to is any good.** If the name exists, it passed the checks that were
  required when it was published ([research D5](./research.md)). Re-verifying published artifacts is
  a different feature.
- **Which release is current, or deployed.** Nothing is deployed anywhere. `:main` records what was
  most recently published, which is not the same claim.
- **Any relationship between the seven services.** They share a change identifier and nothing else.
  Six published and one failed is a partial release, not a broken sixth of one.
