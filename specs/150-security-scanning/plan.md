# Implementation Plan: Automated security scanning in CI

**Branch**: `chore/293-security-scanning` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md) | **Issue**: #293

## Summary

Four scans, each catching what the others cannot:

| Scan | What it sees | When |
| :-- | :-- | :-- |
| **CodeQL** (`codeql.yml`) | the code's data flow, in C# and TypeScript | every pull request, main, weekly |
| **Vulnerable packages** (`ci.yml`: build and client jobs) | NuGet (with transitives) and runtime npm against advisories | every pull request |
| **Dependabot** (`dependabot.yml`) | newer versions of everything: NuGet, npm, Docker, compose, Actions | weekly |
| **ZAP baseline** (`zap.yml`) | what the running apps send a browser: headers, cookies, banners | every merge to main, on demand |

The first round of findings is triaged in `docs/testing/security-scanning.md` and in `.zap/rules.tsv`.

## Technical Context

**Language/Version**: GitHub Actions YAML; .NET 10 and Node 22, as CI
**Primary Dependencies**:
- `github/codeql-action` v3;
- `zaproxy/action-baseline`;
- Dependabot.
**Storage**: none
**Testing**:
- the workflows run on this feature's pull request (ZAP on demand, against the branch);
- negative controls for both package checks;
- a local ZAP baseline against the compose stack.
**Constraints**:
- least privilege: `security-events: write` only for CodeQL, `contents: read` elsewhere;
- ZAP never opens issues (`allow_issue_writing: false`).
**Scale/Scope**: three workflow files, two CI steps, the rules file, one `package.json` line, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** CI tooling, no service changed. |
| **II. Clean Architecture Layering** | **Not applicable.** No application code. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** |
| **IV. Identity Comes From the Token** | **Not applicable**, though CodeQL's queries cover the same ground: user input reaching a query, a path or a redirect. |
| **V. Evidence Over Assumption** | **Planned.** Each scan is shown running, each package check is shown failing on a planted vulnerable version, and every first-round finding is written down with a decision. A scan whose results nobody read would be an assertion, not evidence. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/150-security-scanning/          the record
.github/workflows/codeql.yml          CodeQL, C# (manual build) and TypeScript (no build)
.github/workflows/zap.yml             the ZAP baseline against the compose stack
.github/dependabot.yml                NuGet, npm, Docker, compose, Actions
.github/workflows/ci.yml              build job: vulnerable NuGet; client job: npm audit
.zap/rules.tsv                        each ZAP rule's decision, with its reason
client/packages/ui/package.json       shadcn to devDependencies
docs/testing/security-scanning.md     what runs, and the first round's triage
```

## Complexity Tracking

No violation to justify.
