# Verification run: 2026-09-27

> Recorded on 2026-09-27, when the specs were checked against specs/001's standard. Tasks T011-T030 of this
> feature were post-merge scenarios, and none had been recorded as run. This file records what could be checked
> without changing anything in the registry, and what is still open.

## What was checked

The check was read-only. The public images were read with `docker buildx imagetools inspect`, which needs no
login. The publish job's log was read with `gh run view --log`.

### Scenario 2 (T012): `:main` moves; an existing `sha-` name does not

1. Before the merge of #179 (commit `7db3a48`), the digests of `sha-52fa3b2` were recorded for all ten images.
   `52fa3b2` was the previous merge.
2. After run `36262676940` published `7db3a48`, all ten were read again.

| Image | `sha-52fa3b2` | `:main` points to |
| :-- | :-- | :-- |
| gateway | unchanged (`sha256:4ebabc6f…`) | `sha-7db3a48` |
| identity | unchanged (`sha256:164dc3c3…`) | `sha-7db3a48` |
| catalog | unchanged (`sha256:8738b91f…`) | `sha-7db3a48` |
| order | unchanged (`sha256:c9ff186b…`) | `sha-7db3a48` |
| inventory | unchanged (`sha256:a1bf88e6…`) | `sha-7db3a48` |
| payment | unchanged (`sha256:1fabc51a…`) | `sha-7db3a48` |
| orchestrator | unchanged (`sha256:db0e650e…`) | `sha-7db3a48` |
| cart | unchanged (`sha256:e60ac3cc…`) | `sha-7db3a48` |
| activity | unchanged (`sha256:a43d3f52…`) | `sha-7db3a48` |
| storefront | unchanged (`sha256:d94b1455…`) | `sha-7db3a48` |

Before the publish, `:main` had the same digest as `sha-52fa3b2`, for catalog `sha256:8738b91f…`. Both names were
therefore observed doing what they are for: the moving name moved, and the permanent name held still.

### Scenario 4, the complete direction (T016, half)

The publish job of run `36262676940` printed `identity: publishing` … `storefront: publishing` for the new commit.
It ended with:

```text
10 of 10 permanent names present - release complete
```

The release is ten images now: the storefront image (specs/051) and Activity (specs/041) joined the seven this
feature was written for.

## What is still open, and why it was not run

| Task | Scenario | Why not run on 2026-09-27 |
| :-- | :-- | :-- |
| T011, T018, T030 | Re-run the publish job for a commit that is already published, and compare digests. This is the exact guarantee and the negative control that the check can refuse | It re-runs a release job on `main`. It is harmless if the check works, and exactly the damage the feature prevents if it does not, so the person decides when |
| T015 | Delete two permanent names and re-run: the two reappear and the others are unchanged | It deletes published package versions, which is the user's call |
| T016 (partial direction), T017, T019, T022 | A partial release reporting `n of 10`; a push that cannot succeed; an unanswerable registry; `force_republish` | Each needs a deliberately broken registry or a manual `workflow_dispatch` |
| T029 | The cost of the extra `manifest inspect` calls | 007's duration numbers are not comparable any more: the release has ten images, not seven |

Scenario 2 above shows one more thing. A later publish that asked the registry about `sha-7db3a48` (absent) did
not touch `sha-52fa3b2`. That is weaker than T011 and does not replace it. T011 asks about a name that already
exists.
