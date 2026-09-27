# Feature Specification: A missing commission rate stops Order at startup

**Feature Branch**: `103-commission-at-startup` | **Created**: 2026-09-27 | **Issue**: #210

**Status**: Merged (#223, 2026-09-27)

**Input**: Issue #210 - "a missing commission rate fails the first checkout, not startup".

## Why

`ConfiguredCommissionRate` refuses a missing or out-of-range `Marketplace:CommissionRate`, but it does so only when it
is first resolved, which is at the first checkout. `Program.cs` resolves two other required settings eagerly
(`ConfiguredShippingOptions`, `ITaxRates`) and not this one. A deployment without the rate starts, reports healthy,
and answers the first customer with a 500. The constitution's configuration rule says such a service must fail at
startup rather than failing every request. `docs/features/marketplace.md` lists this as a known limit.

## User Scenarios & Testing *(mandatory)*

### US1 - An operator learns at startup, not from a customer (Priority: P1)

An operator deploying Order without the rate, or with a rate outside [0, 1), sees the service refuse to start with a
message naming `Marketplace:CommissionRate`.

**Why this priority**: This is the issue.

**Independent Test**: Run Order's required-settings check against a configuration without the rate. It throws, naming
the setting.

**Acceptance Scenarios**:

1. **Given** no `Marketplace:CommissionRate`, **When** Order starts, **Then** it stops with an error naming the setting.
2. **Given** a rate of `1.5`, **Then** it stops, saying the rate must be at least 0 and below 1.
3. **Given** `0.1` (as `appsettings.json` ships), **Then** it starts exactly as before.

### Edge Cases

- **Shipping options and tax rates** keep their existing startup checks. The three checks now sit in one place, so the
  next required setting has an obvious home.
- **Tests that replace the rate** (`TestCommissionRate` in the Order fixture) are unaffected: they never run
  `Program.cs`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Order resolves `ICommissionRate` at startup, beside `ConfiguredShippingOptions` and `ITaxRates`, through
  one method, `RequiredSettings.Check(IServiceProvider)`, which `Program.cs` calls after `Build()`.
- **FR-002**: The errors stay the ones `ConfiguredCommissionRate` already raises. No new wording is needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A test runs `RequiredSettings.Check` over the real `AddInfrastructure` registration and shows that it
  passes with a complete configuration, and throws naming the setting when the rate is missing or out of range, or
  when the shipping options are missing.
- **SC-002**: Removing the commission rate from the check makes that test fail (mutation).
- **SC-003**: The Order container still starts and CI's saga job still passes.

## Assumptions

- `appsettings.json` keeps shipping `0.1`, so no deployment that uses the image's own configuration changes behaviour.

## Out of scope

- Per-seller or per-category rates (a separate known limit).
