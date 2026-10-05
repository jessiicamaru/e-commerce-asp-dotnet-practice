---
description: "Task list for Security headers on both apps"
---

# Tasks: Security headers on both apps

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the image check, the Playwright fixture and ZAP's rules, each with a negative control.

- [x] T001 `security-headers.conf`, included in every location; `server_tokens off`; the Dockerfile copies it
- [x] T002 Playwright `support/test.ts`: every test fails on a CSP, COEP or CORP violation; every spec uses it
- [x] T003 The policy settled by running every flow under it (research D2); COOP, COEP, CORP (D3)
- [x] T004 Caddy: HSTS, no `Server` header; validated, and checked over HTTPS in front of the images
- [x] T005 `verify-storefront-image.sh`: every header on every kind of answer, the exact policy; negative control
- [x] T006 `.zap/rules.tsv`: the header rules FAIL, 10055 WARN; ZAP on both images
- [x] T007 Docs: security scanning, production, the client README, CLAUDE.md, timeline, backlog
- [x] T008 Merged, closes #294 - #325

## Evidence

- **The policy was found by running the shop under it.**
  - The first attempt was `style-src 'self'`. The new fixture failed two flows on Sonner's injected `<style>` (two
    hashes: the element inserted empty, then filled), and every toast was silently unstyled.
  - Both bundles have three style injectors: Sonner, TipTap and React's hoisted styles. Sanitised HTML also carries
    `style` attributes.
  - Hence `style-src 'self' 'unsafe-inline'`, with `script-src 'self'` strict (research D2).
  - That first failing run is the fixture's negative control.
- **Browser flows, with the final headers and the widened fixture** (CSP, COEP and CORP violations):
  - 8/8 on the stub;
  - 3/3 with `PAYMENT_PROVIDER=VnPay`: the round trip to the simulator and back;
  - no violation in any of them.
- **The image check**:
  - both images pass: 8 headers on `/`, a deep link, a hashed asset, `/app-config.js` and `/api`, the exact CSP, and
    no version in `Server`;
  - **negative control**: with the include removed from the `/assets/` location, it fails on that answer, naming all
    eight headers.
- **ZAP baseline**, both images, with the updated rules:
  - `FAIL-NEW: 0`, `WARN-NEW: 1` (10055, accepted), 3 IGNORE, 63 rules passing;
  - before this change: 58 passing, 6 header warnings;
  - with the header rules at FAIL and 10055 still FAIL, the run failed on 10055 alone. That is why it is decided, with
    its reason.
- **Caddy**:
  - `caddy validate` passes;
  - a throwaway Caddy in front of the images, over HTTPS, sends `Strict-Transport-Security: max-age=31536000` and no
    `Server` header on both hosts, with the apps' headers passed through.
