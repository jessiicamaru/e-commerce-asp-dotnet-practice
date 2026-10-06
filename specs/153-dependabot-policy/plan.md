# Implementation Plan: Dependabot proposes only what may be merged

**Branch**: `chore/331-dependabot-policy` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md) | **Issue**: #331

## Summary

Two `ignore` rules and a `major` group per ecosystem in `.github/dependabot.yml`; three pull requests closed.

## Technical Context

**Language/Version**: Dependabot configuration (YAML, version 2)
**Primary Dependencies**: none
**Storage**: none
**Testing**: the YAML parses; Dependabot's next run shows no configuration error
**Constraints**: nothing that may be merged is ignored
**Scale/Scope**: one file, three pull requests closed, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** |
| **II. Clean Architecture Layering** | **Not applicable.** |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable**, though it protects the library that implements it from an unlicensed upgrade. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Pass.** Each ignore names the pull request that showed the need, and the reason. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/153-dependabot-policy/        the record
.github/dependabot.yml              ignores and major groups
docs/testing/security-scanning.md   the policy
```

## Complexity Tracking

No violation to justify.
