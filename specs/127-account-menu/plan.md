# Implementation Plan: One account menu everywhere

**Branch**: `feat/254-account-menu` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #254

## Summary

A `constants/account` list of destinations with icons and label keys, rendered by `UserMenu`, `MobileMenu` and the account page; the account-area pages lose their centring.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: react-router, lucide  
**Storage**: none  
**Testing**: Vitest  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change  
**Scale/Scope**: one list, three renderers  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** The list in `constants/`, drawn by components and a page. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** Roles on the session draw entries; the server decides. |
| **V. Evidence Over Assumption** | **Planned.** A test renders all three for three people and compares; mutation-checked; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/127-account-menu/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/constants/account/index.ts
client/src/components/layout/user-menu
client/src/components/layout/top-bar
client/src/pages/account
client/src/pages/account-two-factor
client/src/pages/open-shop
```

## Complexity Tracking

No violation to justify.
