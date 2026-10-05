# Implementation Plan: Security headers on both apps

**Branch**: `fix/294-security-headers` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md) | **Issue**: #294

## Summary

The headers live in one nginx snippet, `client/nginx/security-headers.conf`, which every location of the template
includes. HSTS lives in Caddy, the one place that speaks HTTPS. Three checks hold them:
- the image check pins the exact policy on every kind of answer;
- a Playwright fixture fails any flow on a CSP or cross-origin-policy violation;
- ZAP's rules make the header findings FAIL.

## Technical Context

**Language/Version**: nginx 1.27 configuration, Caddy 2.10, TypeScript (Playwright)
**Primary Dependencies**: none new
**Storage**: none
**Testing**:
- `verify-storefront-image.sh` on both images, plus a negative control;
- all Playwright flows, stub and VNPay, with the violation fixture;
- ZAP's baseline with the updated rules;
- a throwaway Caddy in front of the images, over HTTPS.
**Constraints**: scripts strictly `'self'`; nothing the shop does today may break
**Scale/Scope**: one snippet, the template, the Dockerfile, the Caddyfile, the image check, the e2e fixture, the rules,
docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each app's own nginx decides its own headers. No service is changed. |
| **II. Clean Architecture Layering** | **Not applicable.** Server configuration, no application code. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** |
| **IV. Identity Comes From the Token** | **Supports it.** The access token lives in memory. A CSP that stops an injected script is what keeps it there. |
| **V. Evidence Over Assumption** | **Planned.** The policy was found by running every flow under it: the first strict attempt broke Sonner's toasts, and the violations were read rather than guessed. Every check is shown failing on a planted gap. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/151-security-headers/                     the record
client/nginx/security-headers.conf              the headers
client/nginx/default.conf.template              server_tokens off; every location includes the snippet
client/Dockerfile                               copies the snippet
server/deploy/Caddyfile                         HSTS, no Server header
.github/scripts/verify-storefront-image.sh      every header on every kind of answer; the exact policy
client/e2e/support/test.ts                      fails a test on a CSP / COEP / CORP violation
client/e2e/*.spec.ts                            use it
.zap/rules.tsv                                  the header rules FAIL; 10055 WARN with its reason
```

## Complexity Tracking

No violation to justify.
