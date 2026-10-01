# Implementation Plan: A real not-found page

**Branch**: `fix/243-not-found-page` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #243

## Summary

A `NotFoundPage` in `pages/not-found` replaces the route table's `<p>Not found.</p>`: heading, sentence, a search form navigating to `/?q=`, and a link home, all from `common.notFound.*` in both languages.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: react-router, react-i18next  
**Storage**: none  
**Testing**: Vitest + Testing Library  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change  
**Scale/Scope**: one page  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** A page in `pages/`, routed in `routes/`. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request. |
| **V. Evidence Over Assumption** | **Planned.** Page tests in both languages, mutation-checked; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/120-not-found-page/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/pages/not-found/index.tsx (+ test)
client/src/routes/index.tsx
client/src/locales/{en,vi}/common.json
```

## Complexity Tracking

No violation to justify.
