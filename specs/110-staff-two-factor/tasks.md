---
description: "Task list for Staff sign in with a second factor"
---

# Tasks: Staff sign in with a second factor

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The pieces

- [ ] T001 `Totp` (RFC 6238) and `TotpTests` against Appendix B; `TwoFactorSecretProtector` (AES-GCM, `TWO_FACTOR_KEY`) with its startup check
- [ ] T002 Entities, configurations and migration `AddTwoFactor`; repository methods (guarded step, challenge claim, recovery codes)

## Phase 2: Identity (US1-US4)

- [ ] T003 [US1] Setup, confirm and status; recovery codes issued; other sessions end; the confirming session is verified
- [ ] T004 [US2] Login answers `Required` with a challenge or `SetupRequired`; `POST /login/two-factor`; wrong codes count toward the pause
- [ ] T005 [US1] `JwtTokenGenerator` issues staff roles only for a verified session; refresh carries `TwoFactorVerified`; `AuthResponse` fields
- [ ] T006 [US3] New recovery codes; an administrator's reset with `AccessTokensRevoked`, audit and the `TwoFactorReset` email
- [ ] T007 [US4] Turning it off (not staff)
- [ ] T008 `ADMIN_TOTP_SECRET` in `DataInitializer`; the gateway route
- [ ] T009 `TwoFactorTests`: every acceptance scenario, against PostgreSQL

## Phase 3: Storefront

- [ ] T010 The second step on `/sign-in`; `/account/two-factor` with the QR code, confirmation and recovery codes; the staff redirect; words; tests
- [ ] T011 [US3] "Reset two-factor" on the administrator's person page; test

## Phase 4: Everything that signs in as staff

- [ ] T012 `verify-auth.sh` and `verify-saga.sh` compute the code; CI and compose environment; `.env.example`
- [ ] T013 Bruno: the administrator's second step; the moderator's enrolment; the new negative cases
- [ ] T014 Playwright: the administrator's code in `support/api.ts`; the moderator enrolled and signing in through the second step

## Phase 5: Verification and docs

- [ ] T015 Mutations (quickstart Scenario 4), each red; every server suite and the client suite
- [ ] T016 Rebuilt containers; Bruno and the e2e run; the post-design Constitution re-check
- [ ] T017 Docs: `totp-two-factor.md` §7, moderation-and-staff, email, CLAUDE.md, guides, backlog, timeline; `generate_reference.py`
- [ ] T018 Merged as #230, closing #218
