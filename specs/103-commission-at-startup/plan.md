# Implementation Plan: A missing commission rate stops Order at startup

**Branch**: `103-commission-at-startup` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #210

## Summary

Order's required settings are checked in one method, `RequiredSettings.Check`, which `Program.cs` calls after
`Build()`. It now includes `ICommissionRate`, so a missing or out-of-range `Marketplace:CommissionRate` stops the
service at startup instead of failing the first checkout. A test runs the check over the real `AddInfrastructure`
registration.

## Technical Context

- **Order Infrastructure**: `RequiredSettings.cs` (new).
- **Order WebApi**: `Program.cs` calls it in place of two inline resolutions.
- **Tests**: `Ecommerce.Order.Tests/RequiredSettingsTests.cs` (new; needs no database).

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: Microsoft.Extensions.DependencyInjection and Configuration

**Storage**: none

**Testing**: xUnit, run against the real service registration

**Target Platform**: Order (5059)

**Performance Goals**: not applicable; this runs once at startup

**Constraints**: nothing changes for a deployment with a valid rate

**Scale/Scope**: 1 new class, 1 changed line group, 1 test class

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order only; no other service involved. |
| **II. Clean Architecture Layering** | **Pass.** The check lives in Infrastructure, beside the registrations it resolves; the WebApi only calls it. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Nothing is written or published. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request is involved. |
| **V. Evidence Over Assumption** | **Planned.** A test over the real registration, one mutation, and the rebuilt container through `verify-saga.sh`; the evidence is recorded in `tasks.md`. |

This change brings Order into line with the constitution's **Configuration** rule: a required setting that is missing
must fail at startup, not on every request.

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/103-commission-at-startup/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D2
├── data-model.md        # The settings and when each is checked
├── quickstart.md
├── contracts/
│   └── startup.md       # What Order does when it starts
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/marketplace.md` (the known limit goes), CLAUDE.md (the commission
sentence), backlog, timeline.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
