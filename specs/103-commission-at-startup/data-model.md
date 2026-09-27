# Data Model: A missing commission rate stops Order at startup

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

No tables, columns or migrations change. The feature moves when a configuration value is checked, not what is stored.

## Configuration

| Setting | Required | Rule | Checked |
| :-- | :-- | :-- | :-- |
| `Marketplace:CommissionRate` | yes | a decimal, at least 0 and below 1 | **at startup** (was: at the first checkout) |
| `Shipping:Options` | yes | at least one option with prices (specs/011, 098) | at startup (unchanged) |
| `Tax:DefaultRate`, `Tax:Rates` | yes | rates between 0 and 1 (specs/012) | at startup (unchanged) |

## State transitions

None.
