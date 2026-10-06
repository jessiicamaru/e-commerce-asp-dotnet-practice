---
description: "Task list for Dependabot proposes only what may be merged"
---

# Tasks: Dependabot proposes only what may be merged

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

- [x] T001 `dependabot.yml`: ignore MassTransit and postgres majors, with reasons; a `major` group per ecosystem
- [x] T002 Close #315, #329 and #330 with the reason
- [x] T003 Docs: security scanning, CLAUDE.md, timeline, backlog
- [x] T004 Merged, closes #331 - #332

## Evidence

- **The first run** opened 19 pull requests:
  - 5 grouped minor-and-patch;
  - 14 single-package majors;
  - among them MassTransit 9 (#329, #330) and PostgreSQL 18 (#315).
- **Licence, from the packages' own metadata**: MassTransit 8.3.6's nuspec declares `Apache-2.0`. 9.2.1's declares no
  open-source licence and points to `https://massient.com/license`.
- **`dependabot.yml`** parses, with five ecosystems, each with a `minor-and-patch` and a `major` group. MassTransit
  majors are ignored in NuGet, and `postgres` majors in Docker and compose.
- **#315, #329 and #330** were closed, each with its reason, and their branches deleted.
- The configuration takes effect on Dependabot's next run after merge. Its absence of errors is checked there.
