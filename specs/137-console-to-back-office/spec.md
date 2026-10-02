# Feature Specification: The admin and moderator console moves to the back office

**Feature Branch**: `feat/277-console-to-back-office` | **Created**: 2026-10-03 | **Issue**: #277

**Status**: Draft

**Input**: Issue #277, "the admin and moderator console moves to the back office". This is the third step of
[ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md). specs/136 created the back office with only a
sign-in and a greeting. This step moves the console into it.

## Why

Every page staff use to run the shop - fulfilment, payouts, vouchers, categories, delivery, moderation queues, people,
emails, the audit log - still lives in the storefront under `/admin`. Until it moves, the back office is an empty
room, and the storefront still carries a second application inside it. #278, which removes staff roles from
storefront sessions, cannot happen before this move: if it did, the console would stop working the day it shipped.

## User Scenarios & Testing *(mandatory)*

### US1 - Staff run the shop from the back office (Priority: P1)

**Acceptance Scenarios**:

1. **Given** an administrator signed in to the back office, **Then** every console page they had at `/admin/...` is at the same address without `/admin`, behind the same menu, and does the same thing.
2. **Given** a moderator, **Then** they see the pages their role can use, exactly as before (specs/043, 129).
3. **Given** a console page that showed a product, **Then** its link opens the product's page on the storefront in a new tab.

---

### US2 - The storefront no longer carries the console (Priority: P1)

**Acceptance Scenarios**:

1. **Given** the storefront, **Then** it has no `/admin` page. **Given** an old `/admin/...` address (a bookmark, an email, an old notice), **Then** the storefront sends the browser to the same page in the back office.
2. **Given** a staff member on the storefront, **Then** "Go to management platform" on their account page and in the header opens the back office.
3. **Given** a notice whose link points into the console (a failed-email digest, a granted role), **When** it is opened from the storefront, **Then** it opens in the back office. This holds for notices already stored.

---

### US3 - Staff still close a shop (Priority: P2)

Closing a shop was a button on the shop's public page (specs/107). That page is the storefront, which after #278 holds
no staff session.

**Acceptance Scenarios**:

1. **Given** the back office, **Then** a shop can be closed from its approved application (Shops, Approved) and from its seller's row on the people page, with a reason, as before.
2. **Given** the storefront's shop page, **Then** it offers no staff action.

### Edge Cases

- **Components the console shares with the shop.** All 36 components the console uses are also used by the storefront (order rows, parcel actions, the insights panels, the voucher pages...). They move to `packages/core/src/components` rather than being copied.
- **Addresses.** In the console's code, `/admin/x` becomes `/x`, in links, navigations, the menu's highlight (`state.from`) and the tests.
- **App addresses.** Each app learns the other's address at run time (`/app-config.js`), so one image runs anywhere. Development uses defaults: `http://localhost:5173` and `http://portal.localhost:5174`.
- **The product page's answer form for the shop's own products**, shown to staff, stays until #278. After that a storefront session is never staff, and staff answer from the back office's question queue.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The console's pages, its layout and their tests move to `apps/back-office`. Routes lose the `/admin` prefix. The back office's home is the console's home: fulfilment for an administrator, moderation for a moderator.
- **FR-002**: The 36 components both apps use move to `packages/core/src/components`. The storefront keeps only its own.
- **FR-003**: The storefront redirects `/admin/*` to the back office. The header and account page link there, and notice links into the console open there.
- **FR-004**: `core/config/apps` gives each app the other's address, from `window.__APP_CONFIG__` (nginx, `STOREFRONT_URL` and `BACK_OFFICE_URL`) or the development defaults.
- **FR-005**: Closing a shop moves to the back office (approved applications, sellers on the people page).
- **FR-006**: Playwright: the moderator's product approval runs in the back office.
- **FR-007**: Docs: the client README, the back office page, ADR-003 progress, the feature pages' storefront rows, CLAUDE.md.

## Success Criteria *(mandatory)*

- **SC-001**: Every console test passes in the back office (the same tests, moved), and the storefront has no `admin` folder.
- **SC-002**: In a browser, a moderator approves a product in the back office (Playwright against compose), and `/admin/products` on the storefront lands there.

## Assumptions

- No server change. Notice links keep their `/admin/...` paths: the storefront maps them, and the stored ones need the mapping anyway.
- Seller pages (`/shop/*`) stay in the storefront (ADR-003).
