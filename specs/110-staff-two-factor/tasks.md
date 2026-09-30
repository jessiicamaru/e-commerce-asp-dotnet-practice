---
description: "Task list for Staff sign in with a second factor"
---

# Tasks: Staff sign in with a second factor

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The pieces

- [x] T001 `Totp` (RFC 6238) and `TotpTests` against Appendix B; `TwoFactorSecretProtector` (AES-GCM, `TWO_FACTOR_KEY`) with its startup check
- [x] T002 Entities, configurations and migration `AddTwoFactor`; repository methods (guarded step, challenge claim, recovery codes)

## Phase 2: Identity (US1-US4)

- [x] T003 [US1] Setup, confirm and status; recovery codes issued; other sessions end; the confirming session is verified
- [x] T004 [US2] Login answers `Required` with a challenge or `SetupRequired`; `POST /login/two-factor`; wrong codes count toward the pause
- [x] T005 [US1] `JwtTokenGenerator` issues staff roles only for a verified session; refresh carries `TwoFactorVerified`; `AuthResponse` fields
- [x] T006 [US3] New recovery codes; an administrator's reset with `AccessTokensRevoked`, audit and the `TwoFactorReset` email
- [x] T007 [US4] Turning it off (not staff)
- [x] T008 `ADMIN_TOTP_SECRET` in `DataInitializer`; the gateway route
- [x] T009 `TwoFactorTests`: every acceptance scenario, against PostgreSQL

## Phase 3: Storefront

- [x] T010 The second step on `/sign-in`; `/account/two-factor` with the QR code, confirmation and recovery codes; the staff redirect; words; tests
- [x] T011 [US3] "Reset two-factor" on the administrator's person page; test

## Phase 4: Everything that signs in as staff

- [x] T012 `verify-auth.sh` and `verify-saga.sh` compute the code; CI and compose environment; `.env.example`
- [x] T013 Bruno: the administrator's second step; the moderator's enrolment; the new negative cases
- [x] T014 Playwright: the administrator's code in `support/api.ts`; the moderator enrolled and signing in through the second step

## Phase 5: Verification and docs

- [x] T015 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [x] T016 Rebuilt containers; Bruno and the e2e run; the post-design Constitution re-check
- [x] T017 Docs: `totp-two-factor.md` §7, moderation-and-staff, email, CLAUDE.md, guides, backlog, timeline; `generate_reference.py`
- [x] T018 Merged as #230, closing #218

## Evidence

- Identity 231/231 against PostgreSQL: `TotpTests` 20 (RFC 6238 Appendix B's six SHA-1 vectors, the window, replay,
  base32, the key URI, AES-GCM tamper and wrong key, the startup key check) and `TwoFactorTests` 16. Every server
  project green (Payment 28, Activity 40, Orchestrator 18, Gateway 13, Inventory 78, Catalog 257, Cart 19, Order 307).
- Mutations, each red then restored: staff roles without verification; the replay guard in `Match` and, separately,
  in SQL; the window widened to 2 and narrowed to 0; the challenge not single-use; the failure count unchecked; wrong
  codes not counted; the password clearing the count; a recovery code reusable; staff allowed to turn it off; a reset
  of oneself; rotation dropping `TwoFactorVerified`; an unverified session of a 2FA account renewed; a session
  issued before the code; other sessions not ended; the reset sending no email.
  ⚠️ Three survived the first round - the SQL replay guard, the challenge claim and ending other sessions - because
  the sequential tests were already stopped by an earlier check. Two race tests (five rounds each) and a direct
  `RevokedAt` assertion now kill them.
- Client 575/575, `oxlint` and `tsc -b` clean; 11 mutations each red (the second step, spaces in the code, a dead
  challenge, the setup redirect, the provider accepting before the code, the setup flag, the banner, the reset only
  when on and only after confirming, staff turn-off, the session renewed after confirming).
- Through the gateway: Bruno 356/356 requests, 576/576 tests; `verify-auth.sh` (the password alone issues no token;
  a moderator without a second factor is refused by Catalog - SC-001) and `verify-saga.sh` against rebuilt
  containers; Playwright 4/4 in Edge with the moderator signing in through the page's second step; the three seed
  scripts sign in with a code.
- ⚠️ Bruno found the recovery codes answered as `codes` where the contract, the storefront and Bruno read
  `recoveryCodes` - the unit tests on both sides passed, each mocking the other. Fixed (`RecoveryCodesResponse`).
- Post-design Constitution re-check: see [plan.md](plan.md) - no violation found.
