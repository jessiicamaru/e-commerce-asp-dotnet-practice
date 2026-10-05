# Research: The report's top-down documents

## D1. Summarise and link, never copy

**Decision**: each document is a path through the existing pages, with the essentials inline: one diagram, one
table of numbers, one walk. The detail stays where it already lives.

**Rationale**: `docs/` is already the deliverable's detail. A second copy of the production guide or the load-test
tables would drift from the first. The generated reports (`load-test-results.md`, `resilience-results.md`) are
regenerated from runs, and a copy would not be.

**Alternatives rejected**: *one long report document*. The thesis's own chapters will be written from these. A
single file in the repository would duplicate the thesis instead of serving it.

## D2. Test counts are dated

**Decision**: the counts come from CI's build job on 2026-10-05 (the run for `ddfc7d64`), mapped to projects with
`dotnet test --list-tests` where CI's parallel output is ambiguous.

| Project | Tests |
| :-- | --: |
| Order | 371 |
| Identity | 289 |
| Catalog | 268 |
| Inventory | 83 |
| Payment | 53 |
| Activity | 52 |
| ApiGateway | 22 |
| Cart | 19 |
| Orchestrator | 18 |
| **Server, total** | **1,175** |
| Client unit tests (Vitest, 133 files) | 742 |
| Browser flows (Playwright) | 11 |

**Rationale**: counts move with every feature. A dated count, with a way to read the current one, stays true.
