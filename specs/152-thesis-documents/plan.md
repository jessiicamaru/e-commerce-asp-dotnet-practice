# Implementation Plan: The report's top-down documents

**Branch**: `docs/295-thesis-documents` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md) | **Issue**: #295

## Summary

Four documents that read from the top and link down into the existing feature, architecture, testing and
infrastructure pages. They summarise and point; they do not copy.

## Technical Context

**Language/Version**: Markdown with Mermaid
**Primary Dependencies**: none
**Storage**: none
**Testing**: a link check over the four documents; numbers checked against their sources
**Constraints**: every number sourced; nothing implied that was not done
**Scale/Scope**: four documents, the docs index, CLAUDE.md's test counts

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Documentation. |
| **II. Clean Architecture Layering** | **Not applicable.** |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Pass, and the point.** Every figure links to the run, log or record it comes from, and every limitation is stated. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/152-thesis-documents/             the record
docs/overview/architecture-overview.md  the system on one page
docs/guides/deployment.md               a bare server to a running shop
docs/testing/evaluation.md              what was measured and what it showed
docs/guides/demo-script.md              every role, in order
docs/README.md                          links them
```

## Complexity Tracking

No violation to justify.
