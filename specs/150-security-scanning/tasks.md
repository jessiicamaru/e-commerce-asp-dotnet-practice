---
description: "Task list for Automated security scanning in CI"
---

# Tasks: Automated security scanning in CI

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scans run, plus a negative control for each package check.

- [x] T001 `codeql.yml`: C# (manual build) and TypeScript, `security-extended`; pull request, main, weekly
- [x] T002 `ci.yml`: the build job fails on a vulnerable NuGet package; the client job on a high runtime npm advisory
- [x] T003 `shadcn` to `devDependencies`; the runtime audit clean
- [x] T004 `dependabot.yml`: NuGet, npm, Docker, compose, Actions; grouped, weekly
- [x] T005 `zap.yml` and `.zap/rules.tsv`; a local baseline of both images with the rules
- [x] T006 Negative controls for both package checks
- [x] T007 The CodeQL alerts on this pull request, triaged
- [x] T008 Docs: `docs/testing/security-scanning.md`, testing strategy, docs index, CLAUDE.md, timeline, backlog
- [x] T009 Merged, closes #293 - #309

## Evidence

- **CodeQL** on #309, at `security-extended`: no alerts.
  - C#: 63 queries over the built solution.
  - TypeScript: 103 queries.
  - **Negative control**: a planted SQL injection (a Catalog action concatenating a query-string value into
    `CommandText`) and a DOM XSS (a URL parameter into `innerHTML`), pushed to the same pull request. CodeQL raised
    `cs/sql-injection` (high) and `js/xss` (high). The plant was reverted in the next commit.
- **Vulnerable packages**:
  - NuGet lists nothing.
  - The runtime npm audit was clean once `shadcn` moved to `devDependencies`. The lockfile change is only `"dev": true`
    flags.
  - **Negative controls**:
    - `System.Text.Json` 8.0.0 in a test project was listed with GHSA-hh2w-p6rv-4g7w and GHSA-8g4q-xg66-9fp4 (High),
      so the step would fail.
    - `lodash` 4.17.20 in the storefront made `npm audit --omit=dev --audit-level=high` exit 1.
    - Both were restored, and the restored lockfile audits clean.
- **ZAP baseline**, locally against both images and in CI on #309: `FAIL-NEW: 0` on both apps, with 58 rules passing.
  - 6 WARNs, every one #294's.
  - 3 IGNOREs, each with its reason.
  - `.zap/rules.tsv` holds 66 rules: 57 FAIL, 6 WARN, 3 IGNORE.
  - **Negative control**: rule 10021 switched to FAIL gives exit 1, with `FAIL-NEW: 1`.
- **Dependabot**: `dependabot.yml` covers NuGet, npm, Docker (both Dockerfiles), compose and Actions. It takes
  effect on main, and its first pull requests are the check.
- **The first round's triage** is in `docs/testing/security-scanning.md` and research D4.
