# Quickstart & Validation: Releasable Versions, and Rollbacks That Survive

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-19

Seven scenarios. Most of this feature lives in a pipeline, so most of the evidence is a pipeline run
and what it left behind — **not** a green tick.

---

## Scenario 1 — A merge leaves seven images *(US1, FR-001, FR-002, SC-001)*

Merge this feature, then:

```bash
SHA=$(git rev-parse --short origin/main)
for s in identity catalog order orchestrator inventory payment gateway; do
  printf "%-14s " "$s"
  docker manifest inspect ghcr.io/jessiicamaru/ecommerce-$s:sha-$SHA > /dev/null 2>&1 \
    && echo "published" || echo "MISSING"
done
```

**Expect**: seven `published`, and **0 rebuilds** to get there.

**Failure looks like**: some published, some missing. A partial release is not a release — check
whether the job fails fast on the first error rather than pushing what it managed.

---

## Scenario 2 — It runs without being rebuilt *(SC-007)*

```bash
docker run --rm -p 5057:8080 \
  -e DB_HOST=host.docker.internal -e CATALOG_DB_PORT=5433 \
  -e DB_PASSWORD=<password> -e JWT_SECRET=<secret> \
  ghcr.io/jessiicamaru/ecommerce-catalog:sha-$SHA
curl -s http://localhost:5057/health
```

**Expect**: healthy, on a machine that has never built this.

**This is the scenario that distinguishes the feature from a green pipeline.** A published image that
cannot be pulled and run is a build artifact with a URL.

---

## Scenario 3 — A pull request publishes nothing *(FR-006, SC-004)*

Open any pull request. Let CI finish.

```bash
docker manifest inspect ghcr.io/jessiicamaru/ecommerce-catalog:sha-$(git rev-parse --short HEAD)
```

**Expect**: not found.

**Failure looks like**: it exists. The event guard is wrong, and every proposal is now publishing —
including ones that are never merged.

---

## Scenario 4 — A failing check publishes nothing *(FR-005, SC-003)*

Break a test deliberately on a branch, merge it **to a scratch branch, not `main`**, or watch a real
failing run.

**Expect**: no image for that commit.

**This is the dangerous one to get wrong**, more than scenario 3. An artifact from a failing run
exists, looks exactly like a good one, and carries the implication that it passed.

---

## Scenario 5 — Tags are immutable *(FR-003, SC-002)*

```bash
docker manifest inspect ghcr.io/jessiicamaru/ecommerce-catalog:sha-$SHA \
  | python -c "import sys,json; print(json.load(sys.stdin)['config']['digest'])"
```

Record the digest. Re-run the publish job for the same commit. Inspect again.

**Expect**: the same digest.

**Also confirm the opposite for `:main`** — merge something else, and `:main` moves. That is what
makes it unusable for naming a version to go back to, and why the contract says so.

---

## Scenario 6 — The credential scan runs on what is published *(FR-007)*

Read the job log.

**Expect**: for each service, a build, then `read N filesystem layer(s)` from the scan, then a push —
**in that order**, seven times.

**Failure looks like**: a push before a scan, or one scan for seven images. Both mean the guarantee
attaches to bytes nobody receives.

**And this is public.** A credential in a published layer is readable by anyone and must be
**rotated**, not deleted — removing the package does not recall a copy somebody pulled.

---

## Scenario 7 — The schema check, in both directions *(US2, FR-008, FR-010, SC-005, SC-006)*

**Do not skip the second half.** A check that cannot fail is an absent check wearing a green tick,
and this repository shipped exactly that five days ago in the secret scanner.

| Control | Expect |
| :--- | :--- |
| A PR adding a migration with `DropColumn` | a comment naming the file, the column, and expand/contract |
| A PR adding a migration with only `AddColumn` | **nothing** — no comment, no annotation |
| A PR with no migration at all | **nothing** |
| Any of the above | the job is green either way — it never blocks (FR-009) |

The quickest positive control is the real one already in the repository:

```bash
git show 504fb76 -- '*/Migrations/*ReplaceProductStockQuantityWithAvailability.cs' | grep DropColumn
```

**Also check the count.** The comment states how many migration files it examined. Examining zero and
saying nothing looks identical to examining ten and finding nothing — unless it tells you which.

---

## What passing all seven does not prove

- **That anything is deployed.** Nothing is. These are artifacts; there is no environment, no
  promotion, no rollback command. Deliberately out of scope, because acceptance criteria for them
  could not be checked today.
- **That a rollback would work.** It proves an older image *exists* and that a breaking schema change
  is *visible at review*. Whether a given rollback survives depends on what has been released since —
  which is the question the check exists to raise, not to answer.
- **That the schema check catches everything.** It reads a diff. Raw SQL in `migrationBuilder.Sql`
  is invisible to it, and it cannot tell a widening `AlterColumn` from a narrowing one. Both limits
  are stated in [schema-compatibility.md](./contracts/schema-compatibility.md) rather than left to be
  discovered.
