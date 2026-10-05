# Feature Specification: Automated security scanning in CI

**Feature Branch**: `chore/293-security-scanning`
**Created**: 2026-10-05
**Status**: Draft
**Issue**: #293
**Input**: security rests on the code's own tests (authorization, ownership 404s, guarded updates, rate limits). Nothing
scans the code, the dependencies or the running stack. The thesis needs a standing answer to "how do you know there is
no known vulnerability in what you ship?"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The code is scanned on every pull request (Priority: P1)

A pull request that introduces a known-vulnerable pattern in C# or TypeScript (an injection, an open redirect, a path
built from input) is flagged before it merges, beside the tests.

**Why this priority**: it is the scan nearest the change, and it is free for a public repository.

**Independent Test**: the CodeQL workflow runs on this feature's own pull request for both languages, and its first
alerts are triaged.

**Acceptance Scenarios**:

1. **Given** a pull request, **When** CI runs, **Then** CodeQL analyses C# and TypeScript and reports to the
   repository's code-scanning alerts.
2. **Given** main, **When** a week passes, **Then** CodeQL runs again, because new queries find old code.

### User Story 2 - A known-vulnerable dependency is caught (Priority: P1)

A dependency with a published advisory, among the shop's runtime packages, fails the pull request that adds it.
Dependabot proposes updates for every ecosystem the project uses: NuGet, npm, Docker images, compose images and GitHub
Actions.

**Why this priority**: most real-world compromises of a project like this one come from a dependency, not from its own
code.

**Independent Test**:
- `dotnet list package --vulnerable --include-transitive` finds nothing;
- `npm audit --omit=dev --audit-level=high` finds nothing;
- both run in CI;
- `dependabot.yml` names every ecosystem.

**Acceptance Scenarios**:

1. **Given** a NuGet package with a known vulnerability, directly or transitively, **When** CI builds, **Then** the
   build fails and names it.
2. **Given** a runtime npm package with a high or critical advisory, **When** CI builds the client, **Then** it fails.
3. **Given** a new version of any dependency, **When** Dependabot's weekly run finds it, **Then** it opens one grouped
   pull request per ecosystem, which CI tests like any other.

### User Story 3 - The running apps are scanned from outside (Priority: P2)

On every merge to main, OWASP ZAP's baseline (passive) scan crawls the storefront and the back office served by their
own images in the compose stack, as a browser would reach them. The job fails on any rule marked FAIL. Every other
finding is recorded with a decision.

**Why this priority**: the code and dependency scans cannot see what the server sends: headers, cookies, cache rules,
version banners. The first round of findings is the input to #294 (security headers).

**Independent Test**: `zap-baseline.py` against both images. Every finding appears in `.zap/rules.tsv` with a
decision and a reason.

**Acceptance Scenarios**:

1. **Given** a merge to main, **When** the ZAP job runs, **Then** both apps are scanned and the HTML reports are kept
   as artifacts.
2. **Given** a finding marked FAIL in the rules file, **When** it appears, **Then** the job fails.
3. **Given** a finding not yet fixed (tracked by #294), **When** it appears, **Then** it is reported as WARN and does
   not fail the job.

### Edge Cases

- **A dev-only tool with an advisory.** The `shadcn` CLI pulls `braces` through `fast-glob`, and the runtime-only
  audit must not count it. `shadcn` is a build-time dependency: its CSS is compiled in, and its CLI runs on a
  developer's machine. It moves to `devDependencies`, and its advisory is recorded as not applicable, with the reason.
- **A false positive.** ZAP's "suspicious comments" alert matches the words `admin` and `db` in minified code. It is
  IGNOREd in the rules file with that reason, never silently.
- **Dependabot proposes a major upgrade.** CI tests it like any other pull request, and nothing merges without the
  owner. Dependabot's pull requests are proposals.

## Requirements *(mandatory)*

- **FR-001**: CodeQL for `csharp` and `javascript-typescript` runs on pull requests to main, pushes to main and weekly.
- **FR-002**: The build job fails on any vulnerable NuGet package, direct or transitive.
- **FR-003**: The client job fails on any high or critical advisory among runtime npm packages.
- **FR-004**: `dependabot.yml` covers NuGet, npm, Docker (both Dockerfiles), compose images and GitHub Actions, weekly
  and grouped.
- **FR-005**: The ZAP baseline scans both apps on pushes to main (and on demand). Rules are versioned in
  `.zap/rules.tsv`, and the reports are kept as artifacts.
- **FR-006**: The first round of findings is triaged in `docs/testing/security-scanning.md`: what each finding is,
  and whether it is fixed, tracked (with its issue), or not applicable (with the reason).

## Success Criteria *(mandatory)*

- **SC-001**: This feature's pull request shows CodeQL results for both languages, and every alert is triaged.
- **SC-002**: The vulnerable-package checks pass on the current tree and fail on a deliberately vulnerable package (the
  negative control).
- **SC-003**: A local ZAP baseline of both images gives no FAIL, and every WARN is in the rules file with a decision.

## Assumptions

- The repository is public, so CodeQL and code-scanning alerts are free. Turning on Dependabot *alerts* is a
  repository setting for the owner. `dependabot.yml` drives version updates either way.
- ZAP's baseline is passive (spider plus passive rules). An active scan attacks the target and is out of scope for
  CI. If the thesis needs one, it is run by hand against a local stack.
