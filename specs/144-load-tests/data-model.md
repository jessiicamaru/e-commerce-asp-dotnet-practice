# Data Model: Load-test checkout and measure it

No table, column or migration. The scenarios create ordinary data through the API:
- a category and a product (the shop's own, so it needs no review);
- its stock;
- customers with an address;
- orders.

They remove the category and the product afterwards (`DELETE /api/products/{id}`, which also drops the stock row,
specs/024). Customers and orders stay, as after any test run; `seed/clean-test-debris.py` does not touch them.

## The summary file

`server/loadtest/results/<scenario>-<UTC timestamp>.json` is k6's `handleSummary` data. The scenario adds:

| Key | Meaning |
| :-- | :-- |
| `scenario` | `browse`, `checkout` or `race` |
| `machine` | CPU, cores, memory and OS, from `run.sh` |
| `settings` | VUs, duration or iterations, the starting stock |
| `invariants` | the consistency check's readings and whether each held |
