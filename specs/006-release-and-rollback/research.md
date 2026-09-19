# Phase 0 Research: Releasable Versions, and Rollbacks That Survive

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-19

Eight decisions, each with what was chosen, why, and what was rejected.

---

## D1 — Where images go, and how the pipeline is allowed to push

**Decision**: GitHub Container Registry, `ghcr.io/jessiicamaru/ecommerce-<service>`, authenticated
with the built-in `GITHUB_TOKEN` and `permissions: packages: write` on the job.

**Rationale**: No secret to store, and the constitution is explicit that secrets are *"never written
into a workflow file"*. `GITHUB_TOKEN` is minted per run, scoped by the `permissions` block, and
expires with the job. Same account, same access model as the repository.

**Alternatives considered**:

- **Docker Hub.** Familiar and widely readable, and it needs a long-lived access token stored as a
  repository secret. That is not forbidden — a repository secret is not "in a workflow file" — but it
  is a credential to rotate and revoke for no benefit over a registry that needs none.
- **Uploading artifacts to the Actions artifact store.** Free and simple, and wrong: artifacts expire
  by default, are not pullable by a container runtime, and there is no stable address. It would
  produce something kept but not runnable.

---

## D2 — How a version is named

**Decision**: `sha-<short-sha>` as the only tag anything deployable may be named by. A moving `main`
tag is also pushed, marked in the contract as **not deployable**, for the convenience of "give me the
current one".

**Rationale**: FR-003 and FR-004. An immutable tag is what makes *"go back to the version from
before"* a sentence with a referent. `latest` — or any moving tag — makes it meaningless: you cannot
ask for the previous one, because there is only ever one.

The short SHA is chosen over the full one because it is what a person types when they are reading a
commit list and deciding what to go back to, and the full SHA is recoverable from it.

**Alternatives considered**:

- **Semantic versions.** The right answer once there is a release process and somebody assigning
  numbers. Inventing `v0.1.3` from a commit count would be a number with no meaning attached, and a
  meaningless version is worse than an honest commit id.
- **Date-based tags.** Two merges in one day collide, and the collision is silent.
- **`latest` for deployables.** Rejected as above; this is the specific thing FR-004 exists to
  forbid.

---

## D3 — When it publishes, and when it must not

**Decision**: `if: github.event_name == 'push'`, with `needs: [build, auth-smoke]`. Both conditions,
not either.

**Rationale**: The workflow triggers on `push` to `main` **and** on `pull_request` to `main` —
verified, not assumed:

```bash
$ python -c "..." .github/workflows/ci.yml
on: {'push': {'branches': ['main']}, 'pull_request': {'branches': ['main']}}
```

Without the event guard, every proposal would publish (FR-006 violated). Without `needs`, a merge
whose tests failed would still publish something that looks releasable (FR-005 violated) — and the
second is the dangerous one, because the artifact exists and looks exactly like a good one.

**Alternatives considered**:

- **`if: github.ref == 'refs/heads/main'`.** Nearly equivalent and subtly weaker: a pull request
  targeting `main` has `github.ref` pointing at the merge ref, so the guard holds today, but it
  expresses "which branch" when the thing being guarded is "which event".
- **A separate workflow triggered by `workflow_run`.** More isolated, and it decouples publishing
  from the checks that justify it — the thing this decision exists to bind together.

---

## D4 — Scan the image you publish, not one that resembles it

**Decision**: Fold `image-secrets` into the publish job. Each of the seven images is built, then
scanned, and **only then** pushed. A failed scan fails the job and pushes nothing.

**Rationale**: Today `image-secrets` builds its own Catalog image and scans that. Once images are
published, that arrangement scans one set of bytes and ships another. FR-007 would be a statement
about an artifact nobody receives.

This matters more than it would have last week. The repository is public, so a published image is
pullable by anyone. A `JWT_SECRET` in a layer stops being a local mistake and becomes an
internet-wide one — and a credential that has been pulled must be rotated, not deleted.

Build → scan → push is the only order where the guarantee attaches to the artifact.

**Alternatives considered**:

- **Keep both jobs, scan the published images afterwards.** Detects the problem *after* publishing,
  which for a credential is after the damage.
- **Scan one image and trust the rest.** All seven come from one Dockerfile and one `.dockerignore`,
  so a leak in one is a leak in all — the argument is sound, and it is an argument for scanning one
  *before* seven get pushed, not for scanning a different one. Scanning all seven costs seconds
  because the layers are shared.

---

## D5 — Detecting a schema change that strands earlier versions

**Decision**: A script reading the pull request's diff for files under `*/Migrations/*.cs`, matching
the operations that can break a reader — `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`,
`AlterColumn` — and posting a comment naming what it found and what it means. It **never fails the
job**.

`AlterColumn` is reported as *may be breaking* rather than *is breaking*, because widening is safe
and narrowing is not, and the diff alone cannot always tell.

**Rationale**: The user's decision on 2026-09-19, and the middle of three options for a reason. This
repository already carries the lesson in `docs/architecture/microservices-design.md`: a rule that
said *"nothing may read it for an availability decision"* was broken by the very endpoint that
exposed the value, because nothing checked. **"Nothing may read this" is a wish, not a constraint.**

Blocking was rejected for the opposite failure: nothing is deployed anywhere, so a block would fire
on a risk that does not yet exist, be overridden, and teach everyone that overriding it is routine.
A guard people learn to ignore is worse than none, because it also carries authority.

**This check must be negative-controlled.** A check that cannot fail is precisely the defect this
repository shipped in `verify-image-has-no-secrets.sh` five days ago — it reported clean on an image
that provably leaked, because it was scanning the wrong level of nesting. So: a migration that drops
a column must produce the comment, and one that only adds must produce nothing. Both directions,
before the check is believed.

**Alternatives considered**:

- **Comparing EF Core model snapshots between the base and head commits.** Far more accurate — it
  sees the resulting schema rather than the operations — and needs the SDK, a build of both sides,
  and a diff of generated C#. Disproportionate for a check whose job is to make a reviewer look.
- **Matching on the migration's file name.** Names are chosen by whoever writes them and mean
  nothing.
- **Failing the job.** See above; it is the rejected option from the user's clarification.

---

## D6 — Where the reviewer's question actually gets asked

**Decision**: Both places, because they serve different paths.

1. `.claude/skills/gh-pr-create/templates/pr-description.md` — the checklist gains
   *"the previous image can run against this schema, or the change is split"*. **This is the live
   path**: every pull request in this repository was opened with that skill.
2. `.github/pull_request_template.md` — **created**; there is none today. It covers anyone opening a
   pull request through the web interface, where the skill's template never appears.

**Rationale**: Verified rather than assumed:

```bash
$ ls .github/*.md .github/PULL_REQUEST_TEMPLATE*
(no PR template)
```

Putting the question only in the GitHub template would put it where nobody currently looks. Putting
it only in the skill would miss anyone not using the skill. Both is two lines.

**Alternatives considered**:

- **The skill template only.** Cheapest, and it silently assumes every future contributor uses the
  skill.
- **A required checkbox enforced by a bot.** A checkbox somebody must tick to merge is ticked without
  reading, which is worse than absent because it produces a record of an assurance nobody gave.

---

## D7 — Amending the constitution, by its own procedure

**Decision**: Add the rule to **Technology & Implementation Constraints**, beside the existing
Persistence paragraph, through `/speckit-constitution`. Version `1.0.0 → 1.1.0`.

**Rationale**: The constitution states its own amendment procedure and this plan is bound by it:
*"Amend through `/speckit-constitution`. Every amendment records a Sync Impact Report ... Dependent
templates, skills, and runtime guidance MUST be reviewed in the same change."* The versioning policy
makes an added section a **MINOR** bump.

It goes in Technology & Implementation Constraints rather than becoming a sixth Core Principle
because it is a constraint on how a change is shaped, not a claim about what the system is. The
Persistence paragraph immediately above it already governs schema decisions.

**A drift found while reading**: the Service topology paragraph says each service *"pins its own HTTP
port in `app.Run(...)`"*. Feature 005 made that conditional on `ASPNETCORE_URLS`. The Governance
section says a disagreement between the code and this document *"is never resolved by ignoring it"*,
so the same amendment corrects it. That is a PATCH-level correction riding along with a MINOR
addition; the bump is MINOR.

**Alternatives considered**:

- **A sixth Core Principle.** Over-weights it. The principles are about what the system *is*;
  expand/contract is about how a particular kind of change is *shaped*.
- **`CLAUDE.md` only.** That file is explicitly *runtime guidance* subordinate to the constitution,
  and the rule needs to bind a design decision.

---

## D8 — What is kept, and for how long

**Decision**: Keep everything. The rule is written in the contract; nothing deletes automatically.

**Rationale**: FR-013 forbids age alone, and the correct rule — *keep whatever is still deployed,
plus the last N* — is unanswerable while nothing is deployed. Writing a policy whose central term has
no referent would produce a rule that cannot be evaluated, which the project's guardrails treat as
worse than no rule.

Seven images per merge is not a storage problem at this repository's rate, and the version most worth
keeping is usually the last known good one — which is, by definition, old. Age is exactly the wrong
axis.

**Alternatives considered**:

- **Delete untagged versions after 30 days.** The standard recipe, and it deletes the thing you
  reach for in an incident.
- **Keep the last 20 per service.** Defensible, and arbitrary until there is a deployment to
  reference. Recorded as the likely answer once #8's successor exists.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| The schema check reports nothing and is believed | A breaking migration merges with a clean bill of health, exactly like the secret scan did five days ago | Negative control in both directions is a task, not a suggestion. The check also states how many migration files it examined, so examining zero is visible |
| `AlterColumn` produces false alarms | People learn to ignore the comment | It is reported as *may be breaking* with the reason, not as a verdict. A check that overstates is a check that gets ignored |
| Publishing seven images makes the pipeline slow | People stop merging, or start skipping checks | Shared layers across services; measured in the quickstart rather than assumed |
| Images are published and never pulled by anything | Looks like gold-plating later | Stated in the plan: the artifact story and the schema rule both have to exist before the incident, not during it |
| The constitution amendment is made by hand | The Sync Impact Report and version bump are skipped, and the next reader cannot tell what changed | The procedure is named in the tasks, and the constitution is explicit that it is amended through `/speckit-constitution` |
