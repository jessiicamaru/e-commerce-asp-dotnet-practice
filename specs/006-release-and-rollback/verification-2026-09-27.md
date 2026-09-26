# Verification run: 2026-09-27

> Recorded on 2026-09-27, when the specs were checked against specs/001's standard. Tasks T009-T014 of this
> feature were post-merge scenarios, and none had been recorded as run. This file records what could be checked by
> reading, and what is still open. See also [specs/008's run](../008-immutable-release-tags/verification-2026-09-27.md).

The run examined is main's CI run `36262676940`, which published commit `7db3a48` (#179). It was read with
`gh run view --log` and `docker buildx imagetools inspect`. Nothing in the registry was changed.

| Task | Scenario | Result |
| :-- | :-- | :-- |
| T009 | Images present for the commit, 0 rebuilds | **Pass.** The job ended with `10 of 10 permanent names present - release complete`. The job's steps are, in order: `Build and scan every image`, `Log in to GHCR`, `Push every service`, `Confirm the release is whole`. Nothing is built after the scan, and the log has no second `docker build`. The release is ten images since specs/041 and specs/051 added Activity and the storefront, not seven |
| T011 | A pull request publishes nothing | **Pass.** The head commit of #179 before it was squashed, `edbc5ee`, resolves for no image: `docker buildx imagetools inspect ghcr.io/jessiicamaru/ecommerce-catalog:sha-edbc5ee` is "not found", and so is the storefront's |
| T012 | `:main` moves; a published name holds its digest | **Pass for the contrast half.** `sha-52fa3b2` kept its digest on all ten images while `:main` moved to `sha-7db3a48`; the table is in specs/008's run. The re-run half, which re-runs the job for the same commit, is still open: see specs/008 T011 |
| T013 | build -> scan -> push, and each scan reads layers | **Pass.** The step order is as in T009. Each of the ten scans reports its layers: nine report `read 10 filesystem layer(s)` and one reports `read 11 filesystem layer(s)` |
| T010 | `docker pull` one published image and get a healthy service | **Open.** It pulls an image onto this machine's drive C:, which has been short of space before. Not done in a read-only pass |
| T014 | Pipeline duration against T002 | **Recorded, not compared.** The publish job took 291 s on this run. T002's number was for seven images and is not comparable with ten |
