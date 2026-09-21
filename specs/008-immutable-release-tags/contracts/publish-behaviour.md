# Contract: What Publishing Guarantees

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-21

Supersedes the immutability claim in
[006 release-artifacts.md](../../006-release-and-rollback/contracts/release-artifacts.md), which
stated it as a property when nothing enforced it.

---

## The two names, restated

```text
ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>   # permanent
ghcr.io/jessiicamaru/ecommerce-<service>:main              # moving
```

Seven services: `identity`, `catalog`, `order`, `orchestrator`, `inventory`, `payment`, `gateway`.

---

## What is guaranteed

1. **A permanent name, once it resolves, keeps resolving to the same artifact.** Publishing the same
   change again does not alter it. This is now enforced by the pipeline refusing to push over an
   existing permanent name, rather than by nobody having tried.
2. **A release that stopped partway is completed by running it again.** The re-run pushes what is
   missing and leaves what exists untouched. Re-running is the *correct* recovery, not a second
   failure mode.
3. **A momentary registry fault does not produce a partial release.** Each push is retried up to
   three times before the release is abandoned.
4. **A release reports, per service, whether it published or left an existing artifact alone**, and
   closes by asking the registry whether all seven permanent names exist. Success means the release
   is whole, not merely that the steps ran.
5. **Publishing is serialized.** Two releases cannot interleave, so the moving name cannot end up
   pointing at the older of two closely-merged changes.
6. **Nothing published contains a credential.** Unchanged from feature 006: every artifact is built
   and scanned before any is pushed, and a failed scan publishes nothing.

## What is NOT guaranteed

1. **The moving name is not stable.** `:main` moves by design and cannot answer *"the previous
   version"*. Nothing deployable may be named by it.
2. **Two builds of the same change are not byte-identical.** `docker build` embeds timestamps and
   layer metadata. Guarantee 1 works by *not rebuilding over* an existing name, not by producing the
   same bytes twice — and [006 quickstart scenario 5](../../006-release-and-rollback/quickstart.md)
   originally claimed the opposite, which is how this was found.
3. **An already-published artifact is not re-verified.** If the name exists, what is behind it
   passed what was required when it was published. A re-run does not fetch it, compare it or
   re-scan it.
4. **Nothing is deployed.** This makes a version safe to return to. Returning to it is a person's
   act; there is no environment, no promotion and no rollback command.

---

## The decision the pipeline makes

Per service, before pushing the permanent name:

| `docker manifest inspect` says | Meaning | Action |
| :-- | :-- | :-- |
| exit 0 | the name resolves | **skip** — report `already present, left unchanged` |
| exit ≠ 0, stderr contains `manifest unknown` | package exists, tag does not | push |
| exit ≠ 0, stderr contains `denied` | package does not exist yet | push (first release of that service) |
| exit ≠ 0, anything else | the registry could not be asked | **abort the release** |

> **Exit code alone is not enough, and getting this wrong inverts the guarantee.** A DNS failure, an
> expired token and an outage all exit non-zero. Reading those as "absent" would overwrite a
> published release *because the network hiccupped* — using the check that exists to prevent it. The
> four cases above were measured against the real registry, not inferred
> ([research D1](../research.md)).

The moving name is pushed unconditionally, every time, and is never checked.

---

## Known hole

`denied` cannot distinguish *"no such package"* from *"a package exists that this token may not
read"*. The publish job authenticates to its own namespace with a token scoped to it, so in practice
`denied` is the first-publish case.

**If that ever stopped being true, a release would overwrite instead of skipping** — guarantee 1
would fail silently, which is the exact shape of the defect this contract exists to close. It is
stated rather than hidden. The fallback, if it bites, is a direct registry `HEAD` request, which
answers 200 or 404 with no ambiguity.

---

## Deliberate replacement

A permanent name can be overwritten, and only like this:

- Open the workflow manually (`workflow_dispatch`) and set `force_republish` to true.

It is **not reachable by merging**. Nobody overwrites a release by accident; somebody has to choose
to. When it happens, the run records `republished (forced)` for every service it overwrote.

This exists because a guarantee with no escape hatch gets worked around, and the workaround would be
deleting the package — which breaks anything that already pulled it, cannot be undone, and leaves no
record.

---

## What a release run reports

```text
identity      published
catalog       published
order         already present, left unchanged
orchestrator  published
inventory     published
payment       published
gateway       published

7 of 7 permanent names present — release complete
```

A partial release is visible from the same place:

```text
5 of 7 permanent names present — release INCOMPLETE
missing: payment, gateway
Re-running this job will publish them and leave the other five untouched.
```

The closing count is asked of the registry, not counted from the loop. *"The steps ran"* and
*"the release exists"* are different claims, and only the second is worth reporting.
