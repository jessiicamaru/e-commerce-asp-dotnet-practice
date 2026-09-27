# Research: A missing commission rate stops Order at startup

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #210

---

## D1 - One method resolves every required setting

**Decision**: `Ecommerce.Order.Infrastructure.RequiredSettings.Check(IServiceProvider)` resolves
`ConfiguredShippingOptions`, `ITaxRates` and `ICommissionRate`. `Program.cs` calls it where the two inline resolutions
used to be.

**Rationale**: The defect is that a third required setting was left out of a list that lived inline in `Program.cs`,
and top-level statements in `Program.cs` cannot be tested without starting the whole service and its database. A
method in Infrastructure can be called from a test over the real `AddInfrastructure` registration, so the test fails
if a setting is dropped from the list. It also gives the next required setting an obvious place to go.

**Alternatives considered**:

- **Add a third inline line to `Program.cs`.** Rejected: it fixes this setting, but nothing would test it, and the
  next one could be forgotten the same way.
- **The options pattern with `ValidateOnStart()`.** Rejected for this change: the three settings are validated by
  their own constructors today, and moving them to options classes is a larger refactor than the defect warrants.
- **Starting the service in a test through `WebApplicationFactory`.** Rejected: Order's startup needs PostgreSQL, the
  broker and three gRPC services, all to prove a constructor ran.

---

## D2 - The errors stay as they are

**Decision**: No wording changes. `ConfiguredCommissionRate` already says "Marketplace:CommissionRate is not
configured..." and "...must be at least 0 and below 1."

**Rationale**: Both messages already name the setting and the rule. The only thing wrong was when they were raised.
