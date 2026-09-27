# Quickstart: Validating the startup check

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/startup.md](contracts/startup.md)

## Scenario 1: The test (SC-001)

```bash
cd server
dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~RequiredSettingsTests"
```

**Expected**: all pass, with no database needed. The check passes with a full configuration and throws, naming the
setting, without the rate, with `1.5`, and without shipping options.

## Scenario 2: The mutation (SC-002)

Delete the `ICommissionRate` line from `RequiredSettings.Check` and run Scenario 1. **Expected**: the missing-rate and
out-of-range cases fail. Restore the file (and `touch` it, so MSBuild rebuilds).

## Scenario 3: The service (SC-003)

```bash
cd server
Marketplace__CommissionRate= dotnet run --project src/Services/Order/Ecommerce.Order.WebApi/
```

**Expected**: the process exits at startup with "Marketplace:CommissionRate is not configured...". Without the
override it starts. The rebuilt Order container is healthy, and `verify-saga.sh` passes.
