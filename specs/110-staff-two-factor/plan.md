# Implementation Plan: Staff sign in with a second factor

**Branch**: `110-staff-two-factor` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md) | **Issue**: #218

## Summary

Identity gains TOTP (RFC 6238) with an encrypted secret, recovery codes and a two-step sign-in. Access tokens carry
`Admin` and `Moderator` only for a session verified with a second factor, so every staff endpoint in every service
refuses an unverified one, with no change outside Identity.

The storefront gains:
- the second sign-in step;
- a setup page with a QR code;
- an administrator's reset.

The scripts, Bruno and Playwright compute the administrator's codes from `ADMIN_TOTP_SECRET`.

## Technical Context

- **Identity Domain**:
  - `User` gains the 2FA columns.
  - `RefreshToken.TwoFactorVerified`.
  - `TwoFactorRecoveryCode` and `TwoFactorChallenge`.
- **Identity Infrastructure**:
  - `Security/Totp.cs`, `Security/TwoFactorSecretProtector.cs` (AES-GCM).
  - Repository methods: the guarded step, challenge claims, recovery codes.
  - The migration.
  - `JwtTokenGenerator` filters staff roles.
  - `DataInitializer` uses `ADMIN_TOTP_SECRET`.
- **Identity Application**:
  - `Auth/TwoFactor/*`: setup, confirm, status, recovery codes, disable, the second step, reset.
  - Login and refresh changes.
  - `AuthResponse` fields.
  - `TwoFactorReset` email.
- **WebApi**: routes in `AuthController` and `UsersController`; the key checked at startup.
- **Gateway**: `/api/auth/login/two-factor` with the `sign-in` limiter.
- **Storefront**:
  - the `qrcode` package;
  - sign-in step 2;
  - `/account/two-factor`;
  - the staff redirect;
  - an admin reset button;
  - words;
  - tests.
- **Scripts**: `verify-auth.sh`, `verify-saga.sh`, Bruno `auth/`, `admin-users/`, `admin-insights/`, Playwright
  `support/`; CI and compose environment variables; `.env.example`.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: `System.Security.Cryptography` (HMACSHA1, AesGcm); `qrcode` (storefront)

**Storage**: 3 columns + 1 column + 2 tables in `ecommerce_identity_db`

**Testing**: xUnit against real PostgreSQL (RFC vectors, guarded writes); Vitest; Bruno; Playwright; CI scripts

**Target Platform**: Identity (5056), the gateway, the storefront

**Performance Goals**: one HMAC per window checked; one extra round trip at sign-in for 2FA users

**Constraints**:
- No service but Identity changes.
- The secret is never logged, audited or returned after setup.
- A code works once.

**Scale/Scope**: 7 endpoints, 2 tables, 1 email, 3 pages

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Enforcement is the token's roles, which every service already honours. No service but Identity changes. |
| **II. Clean Architecture Layering** | **Pass.** `ITotp` and `ITwoFactorSecretProtector` are in Application, and their implementations in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The replay step, the challenge claim and a recovery code are each spent by one guarded `UPDATE`. The session is written with its audit entry in one save. The reset's `AccessTokensRevoked` and email are staged before the save. |
| **IV. Identity Comes From the Token** | **Pass.** Every "me" route reads the caller from the token. The second step identifies the account by the challenge, not by an id in the body. |
| **V. Evidence Over Assumption** | **Planned.** The RFC vectors, guarded-write tests against PostgreSQL, mutations, Bruno through the gateway, the CI scripts and Playwright. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/110-staff-two-factor/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
docs/features/auth/totp-two-factor.md   (how TOTP works; §7 filled in by this feature)
```

### Source Code (repository root)

```text
server/src/Services/Identity/…/Auth/TwoFactor/          (new)
server/src/Services/Identity/…/Security/Totp.cs, TwoFactorSecretProtector.cs
server/tests/Ecommerce.Identity.Tests/TotpTests.cs, TwoFactorTests.cs
client/src/pages/sign-in, pages/account-two-factor (new), pages/admin-users
.github/scripts/verify-auth.sh, verify-saga.sh; bruno/; client/e2e/support/
```

## Complexity Tracking

No constitution violation.

The cost worth recording is that **every automated sign-in of staff now computes a code**: the CI scripts, Bruno and
Playwright. That cost is paid deliberately (research D9). Switching 2FA off for CI would test a different system.
