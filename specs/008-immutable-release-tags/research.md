# Phase 0 Research: An Identifier That Never Changes What It Means

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-21

Seven decisions, each with what was chosen, why, and what was rejected. Everything load-bearing was
measured against the real registry rather than assumed.

---

## D1 — Asking the registry, and the three answers it gives

**Decision**: `docker manifest inspect "$IMAGE"` before pushing a `sha-` tag, and classify by the
**error text**, not by the exit code alone.

**Rationale**: Measured against the real registry:

```text
$ docker manifest inspect ghcr.io/.../ecommerce-catalog:sha-2cee71a   # exists
exit=0

$ docker manifest inspect ghcr.io/.../ecommerce-catalog:sha-deadbee   # tag absent
exit=1   manifest unknown

$ docker manifest inspect ghcr.io/.../ecommerce-nosuchsvc:sha-2cee71a # package absent
exit=1   Get "https://ghcr.io/v2/.../manifests/...": denied

$ docker manifest inspect ghcr.invalid.example/foo/bar:baz            # cannot reach
exit=1   failed to configure transport: ... no such host
```

**Exit 1 means three different things, and only two of them mean "safe to push".** Treating them
alike is how a network blip becomes an overwrite — which is exactly the guarantee this feature
exists to provide, destroyed by the check meant to provide it.

| Result | Meaning | What the release does |
| :-- | :-- | :-- |
| exit 0 | the name already refers to something | **skip**, and say so |
| `manifest unknown` | the package exists, this tag does not | push |
| `denied` | the package does not exist yet | push (first release of that service) |
| anything else | the registry could not be asked | **abort**, do not guess |

This repository has already paid for an exit code that meant two things: `curl` printing `000` and
a `|| echo 000` printing another, so an absent service was reported as `HTTP 000000`. That was
cosmetic. This one would be silent data loss.

**The residual risk, stated rather than hidden**: `denied` cannot distinguish *"no such package"*
from *"no permission to read it"*. The job authenticates to its own namespace with a token scoped
to it, so in practice `denied` is the first-publish case. If that ever stopped being true, the
release would overwrite rather than skip. It is the one hole left in FR-001 and it is recorded in
the contract.

**Alternatives considered**:

- **The GHCR REST API** (`/user/packages/container/<name>/versions`). Gives a structured answer
  instead of a string match, and needs a different token scope, pagination, and a name mapping
  between package names and image references. A string match on three known messages is smaller and
  is verified above.
- **Registry v2 HTTP directly** (`HEAD /v2/<name>/manifests/<tag>`). The cleanest possible signal —
  200 against 404 — and it means fetching a bearer token by hand from the `WWW-Authenticate`
  challenge. Worth it if the string matching ever proves brittle.
- **GHCR's own immutable-tag setting.** The right answer if it exists, because a registry refusing
  an overwrite beats a script declining to attempt one.

  **Checked (T001), and the answer is already in the evidence**: GHCR *accepted* an overwrite on
  2026-09-21 — `sha-2cee71a` went from `b7bfe138` to `8d98ec97` on a second push. Whatever the
  setting's availability, it is **not in force on these packages**, so the pipeline check is needed.

  Whether it *could* be switched on is **unverified**: reading package settings needs the
  `read:packages` scope, which this token does not carry (`gh auth refresh -s read:packages` would
  grant it). Left unverified deliberately rather than guessed. If it can be switched on, do both —
  the registry as the guarantee, the pipeline check so a release reports a skip instead of failing
  on a rejected push.

---

## D2 — Skip applies to the permanent name only

**Decision**: `sha-<short-sha>` is checked and skipped when present. `:main` is pushed
unconditionally, every time.

**Rationale**: FR-005. The two names exist to mean different things, and the contract already says
so: `:main` moves and therefore cannot answer *"the previous version"*. Making it immutable would
destroy its only purpose; leaving `sha-` mutable destroys the scheme.

The run's record must show the difference, because a reader who sees "skipped" everywhere needs to
know something still happened.

**Alternatives considered**:

- **Skip both.** Simpler, and it breaks the convenience name for no benefit.
- **Drop `:main` entirely.** Tempting — it is the only mutable thing left — and it is genuinely
  useful for *"give me the current one"*, which is a different question from *"give me the previous
  one"*.

---

## D3 — Retry the push, with a bound

**Decision**: retry each push up to 3 times, sleeping 5s then 15s, and report each retry.

**Rationale**: `unknown blob` was transient — a re-run with identical inputs succeeded. It appears
when `docker push` cross-mounts a layer from a sibling GHCR package (`Mounted from
jessiicamaru/ecommerce-order`), and all seven images share their base layers, so every service after
the first is exposed to it.

Unretried, one hiccup produced defect B: three of seven published and a failed job.

**It did not recur on the next merge.** Run 35592239122 (`f0e203d`) pushed all seven without
incident. Two observations of a fault that appeared once and then did not is exactly the shape that
gets dismissed as a fluke and then costs a day six weeks later — which is the argument for retrying
rather than for waiting to understand GHCR's mount handling.

The bound matters as much as the retry. Retrying forever turns a registry outage into a job that
never ends, and a pipeline nobody can read the state of is worse than one that fails. Three
attempts covers a hiccup; a fourth would be waiting out an outage, which is a person's decision.

**Alternatives considered**:

- **Retry the whole job.** That is what a human re-run does, and with D1 in place it is *safe* —
  but it needs a person to notice. The point of retrying in the step is that the common case does
  not need one.
- **`docker push --disable-content-trust` or pulling base layers first** to avoid cross-mounting.
  Addresses the cause rather than the symptom, and the cause is inside GHCR's mount handling, which
  is not ours to fix. Retry is the honest answer to somebody else's intermittent fault.

---

## D4 — Serialize publishing, so two runs cannot interleave

**Decision**: a `concurrency` group on the publish job, **without** `cancel-in-progress`.

**Rationale**: The spec's first edge case. Two accepted changes merged close together produce two
runs that both push `:main`, and their order is whatever the runners happen to do — so `:main`
can end up pointing at the older of the two. D1 protects the permanent names; nothing protects the
moving one.

`cancel-in-progress: false` is the important half: cancelling a publish mid-loop is precisely how a
partial release happens, and this feature exists to stop producing those.

**Alternatives considered**:

- **Nothing.** The current state. The window is small and the failure — `:main` pointing at the
  older release — is silent and confusing, which is the profile of every bug this project has spent
  a day on.
- **Cancel the older run.** The default recipe, and wrong here for the reason above.

---

## D5 — An existing name is taken at its word

**Decision**: if the name exists, the release skips it. It does not fetch it, compare it, or judge
it.

**Rationale**: The spec's assumption, and the edge case it deliberately left for this plan: *what if
the name exists but what is behind it is incomplete?*

Answering it properly means re-verifying a published artifact on every re-run — fetching every layer
and re-scanning it — which is a larger feature that pays for itself only if half-written artifacts
are common. They are not: a push either completes and the manifest is written, or it does not and
the tag never appears. The manifest is written last.

So the rule is: **the name existing is the evidence.** It passed what was required when it was
published.

**Alternatives considered**:

- **Compare the existing digest with a freshly built one.** Would detect a mismatch and can never be
  acted on, because builds are not reproducible — the digests differ every time. It would report a
  difference on every single re-run.
- **Re-scan the existing artifact for credentials.** Defensible, and it belongs to a "verify what is
  already published" feature rather than to this one.

---

## D6 — Replacing on purpose stays possible, and costs a deliberate act

**Decision**: add `workflow_dispatch` with a boolean `force_republish` input, default false. Only a
manual run with it set to true will overwrite an existing `sha-` tag, and the run's record says
loudly that it did.

**Rationale**: FR-008. A guarantee with no escape hatch gets worked around, and the workaround here
would be deleting the package from the registry — which is worse in every way: it breaks anything
that had pulled it, it cannot be undone, and it leaves no record.

`workflow_dispatch` is the right shape because it cannot be reached by merging. Nobody overwrites a
release by accident; somebody has to open the Actions tab, choose the workflow, and tick a box.

**Alternatives considered**:

- **A magic string in the commit message.** Reachable by merging, which is exactly what must not be
  possible.
- **No escape hatch at all.** Purest, and it makes deleting the package the only option.

---

## D7 — The run says what it did, per service, and whether the release is whole

**Decision**: the job summary gains one line per service — `published` or `already present, left
unchanged` — and a closing count, `7 of 7 present`. The job additionally **verifies at the end**
that all seven names exist, and fails if they do not.

**Rationale**: FR-003 and FR-007, and the same lesson as `read N filesystem layer(s)` in the secret
scanner: a run that did nothing must not look like a run that found nothing to do.

The closing verification is the part worth having. With skipping in place, "the job succeeded" no
longer implies "everything was published" — it could mean everything was skipped, which is fine, or
that a service was quietly never reached, which is not. Asking the registry at the end turns *"the
job did its steps"* into *"the release is complete"*, which is the thing anybody actually cares
about.

**Alternatives considered**:

- **Count what the loop pushed.** Counts the process rather than the result, and the result is what
  a release is.
- **Say nothing when skipping.** Cheaper and indistinguishable from a broken loop.

---

## Documents that currently assert something untrue

Found by re-reading them against what happened, and all four must change with the code:

| Document | What it says | What is true |
| :-- | :-- | :-- |
| [006 quickstart](../006-release-and-rollback/quickstart.md) scenario 5 | "Re-run the publish job for the same commit. Inspect again. **Expect**: the same digest." | It was not the same digest. The scenario is a good test with the wrong expectation recorded — and it found a real bug the first time it was run. |
| [006 release-artifacts contract](../006-release-and-rollback/contracts/release-artifacts.md) | states immutability as a property | It was a property of the naming convention, not of the pipeline. After this feature it is enforced, with the `denied` hole named. |
| `CLAUDE.md` § Published images | "immutable; the only deployable name" | True only after this feature, and the mechanism should be named so the next person knows what enforces it. |
| `ci.yml` "Push all seven" comment | "ALL SEVEN are built and scanned BEFORE ANY is pushed. […] a partial release is not a release" | True of build→scan. Says nothing about the push loop, which produced a partial release the same day. Same shape as the `fail-fast` comment corrected during feature 006. |

FR-010 asks that the falsified scenario record that it was wrong rather than being quietly
rewritten. That is deliberate: a test that was corrected without saying why looks like it always
said that, and the next person loses the evidence that the scheme had a hole.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| `denied` masks a readable-but-existing package | The release overwrites instead of skipping — the exact failure this feature prevents | The token is scoped to its own namespace, so `denied` is the first-publish case. Named in the contract rather than assumed away; D1 lists the HTTP-level check as the fallback if it ever bites |
| The skip makes a broken release look fine | "Nothing to do" and "nothing done" become indistinguishable | D7's closing verification asks the registry whether all seven exist, so success means the release is whole rather than that the steps ran |
| Retry hides a real, persistent registry fault | Slower failures, and a log nobody reads | Bounded at 3, and each attempt is reported so a pattern is visible |
| GHCR gains native immutable tags and this becomes redundant | Harmless duplication | D1 says to check first; if it exists, this feature shrinks to the retry and the reporting |
| Force-republish is used casually | The guarantee erodes quietly | It is unreachable by merging, and the run says loudly that it overwrote |
