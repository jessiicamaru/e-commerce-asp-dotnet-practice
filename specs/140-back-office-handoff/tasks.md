---
description: "Task list for A one-time handoff from the storefront to the back office"
---

# Tasks: A one-time handoff from the storefront to the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity and the gateway (US2)

- [x] T001 `BackOfficeHandoff`, its configuration, migration `AddBackOfficeHandoffs`; personal-data inventory and erasure
- [x] T002 Issue and redeem handlers, the guarded claim, the endpoints; audit
- [x] T003 The gateway's redeem route under the sign-in limit
- [x] T004 `HandoffTests` against PostgreSQL

## Phase 2: The apps (US1)

- [x] T005 Storefront: "Management platform" issues a handoff and opens the callback
- [x] T006 Back office: `/auth/callback`; the sign-in form can start at the code step
- [x] T007 Vitest for both

## Phase 3: Verification and docs

- [x] T008 Bruno; Playwright crossing from the storefront; mutations
- [x] T009 Docs: TOTP page, back office page, ADR-003 progress, CLAUDE.md, reference, timeline, backlog
- [x] T010 Merged, closes #279 - #286

## Evidence

- **Identity, against PostgreSQL**: `HandoffTests` (6):
  - the full crossing: issue, redeem for a challenge with no token and no roles, then the code, ending with `Admin` in the token;
  - a handoff works once (the second use is a 400 on `Code`);
  - an expired code and a made-up one get the same refusal;
  - it lives 30 seconds, and only its SHA-256 is stored;
  - only staff with two-factor sign-in are given one (`NotStaff` / `TwoFactorSetupRequired`, no row written);
  - ten redemptions at once yield exactly one challenge.
  - `MyData` and `AccountDeletion` pass with `back_office_handoffs` declared as withheld and erased.
- **Server mutations**, each red:
  - H1: claimed twice.
  - H2: never expires.
  - H3: issued to anybody.
  - H4: issued without two-factor sign-in.
  - H5: the plain code stored.
  - H6: a five-minute life.
- **Client**: Vitest 728.
  - `goToBackOffice` uses the fragment, and opens the plain back office when no code can be had.
  - The callback removes the fragment, redeems once and asks only for the code. A refusal, or no code, shows the ordinary sign-in. A signed-in back office spends nothing.
  - The user menu crosses with a handoff.
  - The audit-label test caught the new `BackOfficeHandoffIssued` action, which now has words in both languages.
- **Client mutations**, each red:
  - C1: the code in the query string.
  - C2: the fragment left in the address.
  - C3: a signed-in back office spends the code.
  - C4: the menu skips the handoff.
- **Bruno**: 407/407, including `auth/` 14-17 (customer 403 `NotStaff`, issue, a challenge not a session, the second use is 400).
- **Playwright** (Identity, gateway and both apps rebuilt): a moderator signed in to the storefront clicks "Management platform" on `/account`, the back office asks only for the code, and they land on `/moderation`. The flow first failed on two matching links (header and page), so the click is now scoped to the page.
- **Docs**:
  - TOTP page §7, with the crossing's sequence diagram.
  - db-design, personal data, the back office page.
  - ADR-003 progress (complete).
  - CLAUDE.md; the reference (204 endpoints, 58 tables, the gateway route).
