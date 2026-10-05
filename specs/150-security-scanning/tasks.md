---
description: "Task list for Automated security scanning in CI"
---

# Tasks: Automated security scanning in CI

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scans run, plus a negative control for each package check.

- [ ] T001 `codeql.yml`: C# (manual build) and TypeScript, `security-extended`; pull request, main, weekly
- [ ] T002 `ci.yml`: the build job fails on a vulnerable NuGet package; the client job on a high runtime npm advisory
- [ ] T003 `shadcn` to `devDependencies`; the runtime audit clean
- [ ] T004 `dependabot.yml`: NuGet, npm, Docker, compose, Actions; grouped, weekly
- [ ] T005 `zap.yml` and `.zap/rules.tsv`; a local baseline of both images with the rules
- [ ] T006 Negative controls for both package checks
- [ ] T007 The CodeQL alerts on this pull request, triaged
- [ ] T008 Docs: `docs/testing/security-scanning.md`, testing strategy, docs index, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #293

## Evidence

(Filled in when the work is verified.)
