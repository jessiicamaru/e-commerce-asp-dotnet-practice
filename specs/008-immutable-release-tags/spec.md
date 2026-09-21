# Feature Specification: An Identifier That Never Changes What It Means

**Feature Branch**: `008-immutable-release-tags`

**Created**: 2026-09-21

**Status**: Draft

**Input**: Issue [#12](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/12) — "sha- tags are not immutable: re-running publish rewrites an already-released identifier"

## Why this exists

Feature 006 built a way to name a released version and promised the name would never change what it
points at. That promise is the entire value of the scheme: *"go back to the version from before"*
is a sentence with a referent only if the referent holds still.

**It did not hold still for an hour.**

A release ran, failed partway through, and was re-run on the same change. The versions that had
already been published came back as **different artifacts under the same name**:

| Service | What the name meant at 02:35 | What it means now |
| :-- | :-- | :-- |
| identity | `sha256:b7bfe138…` | `sha256:8d98ec97…` |
| catalog | `sha256:25a9242b…` | `sha256:02deb77c…` |
| order | `sha256:8196d2b6…` | `sha256:5bca4688…` |

Read from the registry itself, not from a log.

One of those artifacts had already been fetched by that name, run, and confirmed healthy. An hour
later the name refers to something else, and the thing that was verified no longer answers to the
name it was verified under.

**This is worse than having no identifier at all.** A missing name is obviously missing. A name that
silently changes meaning still looks authoritative, and the moment it matters is an incident.

Three things went wrong together, and they are one capability:

- Nothing enforces that a name keeps its meaning — that was a property of the *convention*, never of
  the process.
- A release can stop halfway and leave some services published and some not, while the process
  claims it cannot.
- The transient failure that caused the halt was never retried, so a momentary registry hiccup
  became a permanent half-release.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A name, once given, keeps its meaning (Priority: P1)

Somebody publishes a change. Later, for any reason, the publishing runs again for that same change.
What the name referred to the first time is what it refers to afterwards.

**Why this priority**: It is the promise the whole scheme rests on, and the one currently broken.
Everything else here is about making that promise survive the ordinary things that go wrong.

**Independent Test**: Record what a name refers to. Publish the same change again. Ask again. The
answer is identical, and the process says plainly that it left the existing one alone.

**Acceptance Scenarios**:

1. **Given** a change that has already been published, **When** publishing runs again for that same
   change, **Then** every already-published name still refers to exactly what it referred to before.
2. **Given** that repeat run, **When** its record is read, **Then** it states for each service
   whether it published or left an existing artifact untouched — silence on that point is not
   acceptable.
3. **Given** the moving convenience name, **When** a later change is published, **Then** that one
   **does** move. The contrast is the point: one name is a promise and the other is a shortcut.

---

### User Story 2 - A release that stops halfway can be finished, not restarted (Priority: P1)

A release fails partway through. Somebody runs it again. It completes what was missing and leaves
what already succeeded alone.

**Why this priority**: Equal first, because without US1 this is impossible and with US1 it is
nearly free — and because the current behaviour is the trap. Today re-running is the obvious
recovery and is also the thing that corrupts the release, which is the worst possible shape for a
recovery procedure.

**Independent Test**: Cause a release to stop after some services are published. Run it again.
Confirm the previously published ones are untouched and the missing ones appear.

**Acceptance Scenarios**:

1. **Given** a release where some services published and others did not, **When** it runs again,
   **Then** the missing ones are published and the existing ones are not replaced.
2. **Given** a release run, **When** it finishes, **Then** it is visible from its record whether
   every service was published or only some — a partial release must never look like a complete one.
3. **Given** a momentary failure from the registry, **When** it happens, **Then** the process tries
   again before giving up, so a hiccup does not become a half-release.

---

### User Story 3 - The written promise matches what is enforced (Priority: P2)

Somebody reading the project's documents learns what is actually guaranteed, not what was hoped for.

**Why this priority**: Third because US1 and US2 fix the behaviour. But several documents currently
state immutability as though something enforced it, and one written check predicted the opposite of
what happened. A document that asserts a guarantee the system does not provide is how somebody comes
to rely on it.

**Independent Test**: Someone unfamiliar with the incident can read the documents and correctly
state what happens when the same change is published twice.

**Acceptance Scenarios**:

1. **Given** the release documents, **When** somebody reads what a version name guarantees, **Then**
   what they read matches what the process does.
2. **Given** the validation scenario that predicted the wrong outcome, **When** it is read, **Then**
   it states the correct expectation and records that its original expectation was falsified.
3. **Given** a statement in the codebase's guidance that a release is all-or-nothing, **When** it is
   read, **Then** it says which part of the process that is true of and which part it is not.

---

### Edge Cases

- **The same change is published twice at the same time.** Two runs racing for the same names must
  not produce a torn result, and neither may quietly replace what the other published.
- **A name exists but the artifact behind it is incomplete or unreadable.** Refusing to replace it
  would strand the release; replacing it silently would break the promise. Something has to give and
  it must be stated which.
- **Only some services are already published.** The common case after a failure, and the one where
  "skip everything" and "replace everything" are both wrong.
- **The registry is momentarily unavailable rather than momentarily confused.** Retrying forever is
  as bad as not retrying; there has to be a bound and it has to be visible when it is reached.
- **Somebody genuinely wants to replace a published artifact.** It should be possible, but never by
  accident and never as the default path.
- **A release is re-run long after the fact**, when the change it refers to is no longer current.
  Nothing about "current" may enter the decision; the name belongs to the change, not to time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Publishing a change that has already been published MUST NOT alter what any existing
  version name refers to.
- **FR-002**: The process MUST determine, per service, whether that version name already exists
  before producing anything under it.
- **FR-003**: When a name already exists, the process MUST leave it untouched and MUST say so in its
  record.
- **FR-004**: A re-run MUST publish the services that are missing, so that re-running is a complete
  and safe way to finish an interrupted release.
- **FR-005**: The moving convenience name MUST still move, and the difference between it and the
  permanent name MUST remain visible in what the process reports.
- **FR-006**: A momentary failure from the registry MUST be retried a bounded number of times before
  the release is abandoned.
- **FR-007**: When a release does not publish every service, that MUST be evident from its record
  without anybody inspecting the registry.
- **FR-008**: Replacing an already-published artifact MUST require a deliberate, explicit act and
  MUST NOT be reachable by ordinary use.
- **FR-009**: Every document that states what a version name guarantees MUST match what the process
  enforces.
- **FR-010**: The validation scenario whose stated expectation was falsified MUST be corrected, and
  MUST record that it was wrong rather than being quietly rewritten.
- **FR-011**: A statement that the release is all-or-nothing MUST name the part of the process it is
  true of.
- **FR-012**: The existing guarantee that a published artifact carries no credential MUST survive
  unchanged.

### Key Entities

- **A version name**: What a released artifact is called. Permanent, tied to the change it was built
  from, and after this feature it means one thing for as long as it exists.
- **A moving name**: A convenience pointing at the most recent release. Explicitly not usable to name
  a version to go back to, and it keeps that property.
- **A release run**: One attempt to publish every service for one change. May succeed completely,
  fail completely, or stop partway — and the third case must be both visible and recoverable.
- **An existing artifact**: One already published under a version name. After this feature it is
  evidence, not a draft: the process reads it and defers to it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Publishing the same change twice leaves **0** existing version names referring to
  something different than before.
- **SC-002**: After a release that stopped partway, one re-run brings the count of published services
  to all of them, with **0** of the previously published ones replaced.
- **SC-003**: Reading a release run's record answers "was every service published?" without opening
  the registry, in **every** case including the partial one.
- **SC-004**: A momentary registry failure no longer produces a partial release; it is retried and
  the release completes.
- **SC-005**: The moving name still refers to the most recent release after a later change is
  published — the guarantee in SC-001 applies to permanent names only, and that distinction is
  demonstrable.
- **SC-006**: Someone who has not seen this incident can state, from the documents alone, what
  happens when the same change is published twice, and be right.
- **SC-007**: The validation scenario that was falsified now predicts what actually happens, and is
  confirmed by running it.

## Assumptions

- **Nothing is deployed anywhere.** This makes a released version *safe to return to*; it does not
  deploy, promote or roll back. Those remain out of scope for the same reason as in feature 006 —
  there is nowhere to deploy, so their acceptance criteria could not be checked.
- **Builds are not reproducible, and making them so is not the answer here.** Two builds of the same
  change differ in timestamps and layer metadata. Chasing bit-identical rebuilds would be a large
  effort to make a guarantee that refusing to overwrite provides directly.
- **The registry is the source of truth about what exists.** The process asks it rather than keeping
  its own record, because a separate record is a second thing that can be wrong.
- **An existing name is treated as correct.** If a name exists, what is behind it passed the checks
  that were required at the time it was published. Re-verifying every existing artifact on every
  re-run would be a different and larger feature.
- **Deliberate replacement stays possible** but out of the ordinary path, because an artifact
  published from a mistake should be removable by somebody who means it.
- **The seven services are still released together**, sharing one change identifier, as established
  in feature 006.

## Dependencies

- The publishing process established by feature 006 exists and works: it builds, scans, and pushes
  seven artifacts on an accepted change. This feature changes *when it refuses to push*, not how it
  builds.
- The credential scan runs before anything is published and must keep doing so.

## Out of scope

- Deploying, promoting between environments, or performing a rollback.
- Making builds reproducible.
- Re-verifying artifacts that were published previously.
- Changing how versions are named, or introducing release numbers.
- Any change to how the services behave.
