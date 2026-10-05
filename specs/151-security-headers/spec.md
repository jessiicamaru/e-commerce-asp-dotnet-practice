# Feature Specification: Security headers on both apps

**Feature Branch**: `fix/294-security-headers`
**Created**: 2026-10-05
**Status**: Draft
**Issue**: #294
**Input**: nginx serves the storefront and the back office with cache headers only: no Content-Security-Policy, no
`X-Frame-Options`, no `Referrer-Policy`. Specs/150's ZAP baseline confirmed it, with six warnings on each app. The
argument for the back office's own origin (ADR-003) is isolation against scripts. A CSP is the other half: it stops a
script that slips in from running at all.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An injected script does not run (Priority: P1)

If markup ever reaches a page unsanitised (a notice, a product description, an email preview), a `<script>` in it, an
inline event handler or a script from another site does not execute. Without that, it would run with the person's
session: a staff member's session in the back office.

**Why this priority**: DOMPurify and the server-side allow-list are the first line. The CSP is the line behind them,
for the day one of them misses something.

**Independent Test**: each app's CSP allows scripts from its own origin only, with no `'unsafe-inline'` and no
`'unsafe-eval'`. Every browser flow passes under it with no CSP violation reported.

**Acceptance Scenarios**:

1. **Given** either app, **When** any page, asset or API answer is served, **Then** it carries the CSP, with
   `script-src 'self'`.
2. **Given** every Playwright flow (storefront, back office, VNPay), **When** it runs under the headers, **Then** the
   browser reports no CSP or cross-origin-policy violation.
3. **Given** the shop's pages, **When** another site tries to frame them, **Then** the browser refuses
   (`frame-ancestors 'none'`, `X-Frame-Options: DENY`).

### User Story 2 - The rest of the hardening headers, and HTTPS kept (Priority: P2)

- Responses are not MIME-sniffed (`nosniff`).
- A link to another site does not leak a page's path (`Referrer-Policy`).
- Powerful browser features nobody uses are switched off (`Permissions-Policy`).
- Each app is cross-origin isolated (COOP, COEP, CORP).
- nginx does not announce its version.
- In production, a browser that has visited once never tries plain HTTP again (HSTS, at Caddy).

**Why this priority**: each closes a known class of attack cheaply. Together they are what ZAP's baseline checks.

**Independent Test**:
- `verify-storefront-image.sh` asserts every header, and the exact policy, on every kind of answer;
- ZAP's baseline has no FAIL;
- Caddy, in front of the images, adds HSTS and no `Server` header.

**Acceptance Scenarios**:

1. **Given** each kind of answer (the app, a deep link, a hashed asset, `/app-config.js`, `/api`), **When** the image
   check asks, **Then** all eight headers are there and the CSP is exactly the agreed one.
2. **Given** the production entrance, **When** a page is fetched over HTTPS, **Then** it carries
   `Strict-Transport-Security` and no `Server` header.
3. **Given** ZAP's baseline, **When** it runs, **Then** the six header rules pass, and they are FAIL in
   `.zap/rules.tsv` from now on.

### Edge Cases

- **A library that injects `<style>` at run time.** Sonner (both apps) and TipTap (the email editor) do, and the
  sanitised HTML of notices and email previews carries `style` attributes. `style-src` therefore allows
  `'unsafe-inline'`, with the reasons in research D2. Scripts never do.
- **A location that sets its own header.** nginx drops the server's `add_header` there, so each location includes the
  headers. The image check asks every kind of answer, so a location that forgets is caught.
- **The VNPay round trip.** Leaving for the gateway is a navigation, so the CSP does not govern it. The three VNPay
  flows pass under the headers.
- **A future cross-origin image** (a CDN). COEP `require-corp` would block it unless it sends CORP, and the e2e fixture
  would report that. Adding a CDN means revisiting COEP.

## Requirements *(mandatory)*

- **FR-001**: Both apps send:
  - a CSP with `default-src`, `script-src`, `connect-src` and `font-src` `'self'`;
  - `style-src 'self' 'unsafe-inline'`;
  - `img-src 'self' data: blob:`;
  - `object-src 'none'`, `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'`.
- **FR-002**: Both apps send `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, a `Permissions-Policy` switching off unused features, and COOP
  `same-origin`, COEP `require-corp` and CORP `same-origin`.
- **FR-003**: `server_tokens off`: no nginx version anywhere.
- **FR-004**: Caddy adds `Strict-Transport-Security: max-age=31536000` and removes its `Server` header, for both hosts.
- **FR-005**: The image check pins the exact policy and asserts every header on every kind of answer.
- **FR-006**: Every Playwright test fails on a CSP or cross-origin-policy violation.
- **FR-007**: `.zap/rules.tsv` marks the six header rules FAIL. The accepted `style-src 'unsafe-inline'` (ZAP 10055) is
  WARN, with its reason.

## Success Criteria *(mandatory)*

- **SC-001**: All 11 browser flows pass with zero violations: 8 on the stub, and 3 with VNPay.
- **SC-002**: The image check passes on both images and fails when one location loses the headers (negative control).
- **SC-003**: ZAP's baseline on both images gives `FAIL-NEW: 0`, with one WARN (10055).
- **SC-004**: Through Caddy over HTTPS, both hosts send HSTS and no `Server` header.

## Assumptions

- Every resource both apps load is same-origin (`/assets`, `/api`, `/app-config.js`, bundled fonts). Checked in the
  built `index.html` and both bundles: no inline `<script>`.
- No email embeds a product image (checked in Identity's email code), so CORP `same-origin` on `/api` breaks nothing
  outside the apps.
