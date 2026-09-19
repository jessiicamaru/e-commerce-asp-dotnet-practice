# Feature Specification: Releasable Versions, and Rollbacks That Survive

**Feature Branch**: `006-release-and-rollback`

**Created**: 2026-09-19

**Status**: Draft

**Input**: Issues [#8](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/8) — "CI produces nothing that can be deployed or rolled back" — and [#9](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/9) — "an older image cannot run against a newer schema"

## Why this exists

The system can now be packaged and run, and the checks that prove it works are green on every
change. Then the pipeline throws away everything it built.

The consequence is that **"which version is running?" has no answer**. There is only a commit that
somebody has to fetch and build again — and a rebuild is not the thing that passed the checks, it is
a new thing that resembles it.

Two halves, and they are one capability:

- Without a kept, addressable version, there is nothing to go back to.
- Without a rule about schema changes, going back to one does not work anyway. A change already
  released on 2026-09-17 removed a field that earlier versions read on every request, so every
  version before it is now broken against the current database.

Publishing versions you cannot safely return to is half a capability, and the missing half is the
one that only announces itself during an incident.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every accepted change leaves something addressable behind (Priority: P1)

A change is merged, the checks pass, and the exact artifact those checks ran against is kept and can
be named.

**Why this priority**: Everything else depends on it. A rule about rolling back is theoretical until
there is something to roll back to.

**Independent test**: Merge a change, then ask for the artifact belonging to that commit and get it —
without building anything.

**Acceptance Scenarios**:

1. **Given** a change that passes every check, **When** it is merged, **Then** one artifact per
   service is kept, each identifiable by the exact change it was built from.
2. **Given** an artifact identified by a change, **When** it is fetched twice, at any interval,
   **Then** it is the same artifact both times — an identifier is never reused.
3. **Given** a change whose checks do **not** pass, **When** it finishes, **Then** nothing is kept.
   A failing pipeline must not leave something that looks releasable.
4. **Given** a proposed change that has not been accepted, **When** its checks run, **Then** nothing
   is kept — the checks may prove the artifact builds, but a proposal does not produce a release.
5. **Given** any artifact kept by this process, **When** it is inspected, **Then** it contains no
   credential — the existing guarantee must not weaken because artifacts are now shared.

---

### User Story 2 - A change that would strand earlier versions is visible at review (Priority: P1)

Somebody proposes a change to the database's shape. Before it is accepted, the reviewer is told,
without having to work it out, whether the previous version of the software could still run.

**Why this priority**: Equal first, and for a different reason. This is the only requirement whose
absence is **silent**. A missing artifact is obvious the moment you look for one; a schema change
that stranded every earlier version announces itself only when somebody tries to go back, which is
the worst possible moment to find out.

**Independent test**: Propose a change that removes a field, and check that the review surfaces the
consequence without anybody thinking to look for it.

**Acceptance Scenarios**:

1. **Given** a proposed change that removes or narrows part of the database's shape, **When** its
   checks run, **Then** the consequence for earlier versions is stated on the proposal itself, in
   terms a reviewer can act on.
2. **Given** such a proposal, **When** the author decides to proceed anyway, **Then** they can —
   the check informs, it does not block.
3. **Given** a proposed change whose schema change is purely additive, **When** its checks run,
   **Then** nothing is raised. Most changes are additive and must stay cheap.
4. **Given** any proposal, **When** it is reviewed, **Then** the reviewer is asked whether the
   previous version can run against the new shape, at the moment they are looking at the change.

---

### User Story 3 - The rule is written down where it binds (Priority: P2)

Somebody planning a schema change can find out, before writing it, what is expected and why.

**Why this priority**: Third because US2 already catches the case at review time. But a check that
explains a rule nobody has stated is an obstacle rather than guidance — and the reviewer needs
something to point at when they say "split this".

**Independent test**: Someone who has never seen this feature can find the rule, understand which
changes it covers, and see what to do instead.

**Acceptance Scenarios**:

1. **Given** the project's governing principles, **When** someone looks for guidance on changing the
   database's shape, **Then** the rule is there, with what counts as breaking and the two-step shape
   that avoids it.
2. **Given** the rule, **When** somebody reads it, **Then** it names the alternative that was
   rejected and why, as the project's governance requires of any recorded decision.
3. **Given** a change already released that breaks this rule, **When** someone reads the record,
   **Then** they find it acknowledged rather than discovering it themselves.

---

### Edge Cases

- **A change is merged while an earlier one is still being processed.** Each must produce its own
  artifact; neither may overwrite the other's.
- **The same change is processed twice.** It must not produce two different artifacts under one
  name.
- **Storage grows without limit.** Every accepted change keeps seven artifacts. Something must say
  what is kept and for how long, or the answer becomes "until it breaks".
- **Deleting an artifact somebody needs.** Age alone is the wrong rule — the version worth keeping
  longest is often the last known good one, which is by definition old.
- **A schema change that is breaking but not obviously so** — a field narrowed rather than removed,
  or a name changed. The check must catch the shape, not the keyword.
- **A proposal with no schema change at all.** By far the common case, and it must cost nothing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every accepted change that passes all checks MUST leave one kept artifact per service.
- **FR-002**: Each artifact MUST be identifiable by the exact change it was built from.
- **FR-003**: An identifier MUST never be reused. Fetching the same identifier at any later date MUST
  return the same artifact.
- **FR-004**: No identifier that moves between artifacts MUST be usable to name a deployable version.
- **FR-005**: Artifacts MUST be kept only after every check passes.
- **FR-006**: A proposal that has not been accepted MUST NOT cause anything to be kept.
- **FR-007**: A kept artifact MUST contain no credential, preserving the existing guarantee now that
  artifacts can be shared.
- **FR-008**: A proposed change that removes, renames or narrows part of the database's shape MUST
  have that fact, and its consequence for earlier versions, stated on the proposal automatically.
- **FR-009**: That statement MUST NOT prevent the change from being accepted.
- **FR-010**: A purely additive schema change MUST raise nothing.
- **FR-011**: Every proposal MUST ask the reviewer whether the previous version can run against the
  new schema.
- **FR-012**: The project's governing principles MUST state the rule, what counts as breaking, and
  the two-step shape that avoids it.
- **FR-013**: A retention rule MUST state what is kept, and MUST NOT be based on age alone.
- **FR-014**: The schema change already released that breaks this rule MUST be acknowledged in the
  record rather than left for someone to discover.

### Key Entities

- **Kept artifact**: One runnable copy of one service, produced by an accepted change and never
  altered afterwards. Its identity is the change it came from.
- **Identifier**: What names an artifact. Permanent and never reused — this is what makes "go back to
  the version from before" a possible sentence.
- **Schema change**: A change to the shape of stored data. Either *additive* — earlier versions keep
  working — or *breaking* — they do not.
- **The two-step shape**: A breaking change split into an additive step that earlier versions survive,
  and a removal step taken later once nothing depends on what is removed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After an accepted change, seven artifacts exist that are identifiable by that change —
  **0 rebuilds** required to obtain them.
- **SC-002**: Fetching an artifact by its identifier twice, days apart, yields byte-identical
  results.
- **SC-003**: A pipeline run whose checks fail leaves **0** artifacts.
- **SC-004**: An unaccepted proposal leaves **0** artifacts.
- **SC-005**: A proposal removing a field states the consequence for earlier versions **without any
  person looking for it**, within the normal time the checks take.
- **SC-006**: A purely additive proposal raises **0** such statements.
- **SC-007**: Given an identifier, there is a documented single command that obtains and runs that
  exact version.
- **SC-008**: Someone unfamiliar with the feature can state, from the written rule alone, whether a
  given schema change is breaking.

## Assumptions

- **The schema check informs rather than blocks** — the user's decision on 2026-09-19. Rejected:
  writing the rule down only, which the project's own documentation argues against ("a rule nobody
  checks is a wish"); and failing the pipeline, which would block acceptance over a risk that does
  not yet exist anywhere, and a block that is routinely overridden teaches people to override it.
- **Nothing is deployed anywhere.** This feature makes versions that *could* be deployed and makes
  going back to one *safe*. Deploying, promoting between environments, and a command that performs a
  rollback are out of scope and cannot have checkable acceptance criteria until somewhere to deploy
  exists.
- **Retention keeps everything, for now.** With nowhere deployed, "what is still in use" has no
  answer, and deleting by age would eventually remove the last known good version. The rule is stated
  and revisited when there is a deployment to reference.
- **The already-released breaking change is accepted, not reverted.** Nothing is deployed, so nothing
  can be stranded by it. It is recorded as the worked example instead — a real one is worth more than
  an invented one.
- **The seven services are released together.** Each produces its own artifact, but they share the
  change they came from. Independent versioning per service is a larger decision nobody has needed
  yet.
- **Artifacts are readable by anyone who can read the source.** This is a public repository; nothing
  here changes who can see what, which is also why FR-007 matters more now than it did.

## Dependencies

- Services are already packageable and run from their configuration, and the existing check that an
  artifact carries no credential already runs on every change. This feature keeps what is built
  rather than making it buildable.
- The checks that must pass first already exist: build with tests, the authentication smoke test, and
  the credential scan.

## Out of scope

- Deploying anywhere, promoting between environments, or a command that performs a rollback (#8's
  own deliberate boundary).
- Independent version numbers per service, or any release-naming scheme beyond identifying the change.
- Reverting the schema change already released.
- Any change to how the services behave.
