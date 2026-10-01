# Implementation Plan: Every audit action has words, and the status page lists every service

**Branch**: `fix/244-staff-labels-status` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #244

## Summary

Add the 54 missing action labels in both languages and a test that scans the server for every action it records; list all eight health routes on `/status` with readable names, held to the gateway's configuration by a test.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: react-i18next  
**Storage**: none  
**Testing**: Vitest (node fs over the server's source and the gateway's appsettings.json)  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change  
**Scale/Scope**: two locale files, one service list, two tests  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client reads the server's source only in a test; nothing at run time couples them. |
| **II. Clean Architecture Layering** | **Pass.** Words in `locales/`, the list in `services/health`. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** Both tests shown red by removing a label and a service; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/121-staff-labels-status/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/locales/{en,vi}/admin.json
client/src/locales/{en,vi}/status.json
client/src/services/health/index.ts
client/src/pages/status/index.tsx
client/src/locales/audit-actions.test.ts
client/src/services/health/index.test.ts
```

## Complexity Tracking

No violation to justify.
