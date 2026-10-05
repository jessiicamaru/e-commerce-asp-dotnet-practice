---
description: "Task list for Security headers on both apps"
---

# Tasks: Security headers on both apps

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the image check, the Playwright fixture and ZAP's rules, each with a negative control.

- [ ] T001 `security-headers.conf`, included in every location; `server_tokens off`; the Dockerfile copies it
- [ ] T002 Playwright `support/test.ts`: every test fails on a CSP, COEP or CORP violation; every spec uses it
- [ ] T003 The policy settled by running every flow under it (research D2); COOP, COEP, CORP (D3)
- [ ] T004 Caddy: HSTS, no `Server` header; validated, and checked over HTTPS in front of the images
- [ ] T005 `verify-storefront-image.sh`: every header on every kind of answer, the exact policy; negative control
- [ ] T006 `.zap/rules.tsv`: the header rules FAIL, 10055 WARN; ZAP on both images
- [ ] T007 Docs: security scanning, production, the client README, CLAUDE.md, timeline, backlog
- [ ] T008 Merged, closes #294

## Evidence

(Filled in when the work is verified.)
