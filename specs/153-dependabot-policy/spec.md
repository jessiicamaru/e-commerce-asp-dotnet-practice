# Feature Specification: Dependabot proposes only what may be merged

**Feature Branch**: `chore/331-dependabot-policy`
**Created**: 2026-10-06
**Status**: Draft
**Issue**: #331
**Input**: the first Dependabot run after specs/150 opened 19 pull requests. Three of them must never be merged:
- MassTransit 9 (#329, #330) is a commercial product that needs a license;
- PostgreSQL 18 (#315) cannot read version 16's data files.

The rest were many single-package majors, one pull request each.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Nothing proposed that must not be merged (Priority: P1)

The owner reads Dependabot's pull requests and merges what passes. A pull request that would breach a license or
make the databases unstartable is never in that list.

**Independent Test**: `dependabot.yml` ignores major updates of `MassTransit*` and of the `postgres` image, and says
why. The next run opens neither.

### User Story 2 - A week's updates fit on one screen (Priority: P2)

Each ecosystem gets at most two pull requests a week: one for minor and patch updates, one for majors.

**Independent Test**: every update entry has a `major` group beside its `minor-and-patch` group.

### Edge Cases

- **A MassTransit 8.x patch** still comes, in the minor-and-patch group. Only `version-update:semver-major` is
  ignored.
- **A PostgreSQL 16 minor or patch image** still comes. Upgrading to 17 or 18 is planned work: a dump and restore per
  database, written down when it is done.
- **A major that breaks the build** arrives with the others in its group. CI fails, and the owner splits it by
  closing the group's pull request and asking Dependabot for one package (`@dependabot recreate` after narrowing the
  group, or a manual bump).

## Requirements *(mandatory)*

- **FR-001**: Ignore `version-update:semver-major` for `MassTransit*` in NuGet, and for `postgres` in Docker and
  compose, each with its reason.
- **FR-002**: Every ecosystem groups majors into one pull request.
- **FR-003**: #315, #329 and #330 are closed with the reason.

## Success Criteria *(mandatory)*

- **SC-001**: The configuration parses, and Dependabot's next run reports no configuration error.
- **SC-002**: The three pull requests are closed, each with a comment naming its reason.

## Assumptions

- MassTransit's licensing, from the packages' own metadata: 8.3.6 declares `Apache-2.0`, while 9.2.1 declares no
  open-source licence and points to `https://massient.com/license`, a commercial one. Moving to 9 is a licensing
  decision for the owner, never an automatic bump.
