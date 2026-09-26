# Implementation Plan: Password reset

> Completed on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md) with
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Branch**: `061-password-reset` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #103

## Summary

Two anonymous endpoints in Identity and two pages in the storefront. Asking for a link stores a random token as
its SHA-256 hash, deletes the person's earlier unused links, records the request and queues a `PasswordReset`
email straight into Identity's own `outgoing_emails` - all in one transaction, so the token never crosses the
broker. Choosing a new password claims the link in one guarded statement, sets the password and revokes every
session in one transaction. The dispatcher scrubs a sent reset email's data. Decisions are in
[research.md](research.md) (decision 45 in [docs/project/decisions.md](../../docs/project/decisions.md)).

## Design

**Identity**

*Domain.* `PasswordResetToken`, stored in `password_reset_tokens`:

| Column | Notes |
| :-- | :-- |
| `Id` | |
| `UserId` | foreign key to users, cascade |
| `TokenHash` | char(64), unique |
| `ExpiresAt` | |
| `UsedAt` | null until used |
| `CreatedAt` | |

*Application* (`Auth/Commands/PasswordReset/`):
- `ForgotPasswordCommand(Email)`, with a `Language` the controller fills from `Accept-Language`:
  - it runs in one transaction and always completes normally;
  - for a real account: delete that account's unused tokens, insert the new one, record
    `PasswordResetRequested`, and queue the `PasswordReset` email with `{ token }`, all saved together.
- `ResetPasswordCommand(Token, Password)`:
  - the validator uses registration's password rules;
  - the handler runs `TryClaimAsync(hash, now)`, which returns a user id or none. None is a
    `ValidationException` on `Token`, so a 400 with a message.
  - After a claim: set the password hash, then `RevokeAllRefreshTokensAsync`, record `PasswordReset`,
    and save, in one transaction. *(As built, the order inside that transaction is: set the hash, record the
    entry, save, then run the bulk revocation - the same transaction, so the outcome is the one planned.)*
- `EmailTemplates.PasswordReset`, in vi and en; the link is `{StorefrontUrl}/reset-password?token=…`.
- The dispatcher: once a `PasswordReset` email is sent, its `DataJson` is replaced with `{}`.

*WebApi.* `AuthController` gains `forgot-password` (202) and `reset-password` (204), both anonymous.

**Storefront**
- `services/auth`: `forgotPassword(email)`, `resetPassword(token, password)`.
- The sign-in page links to `/forgot-password`.
- `pages/forgot-password` shows the same confirmation whatever the answer, except that a network
  failure shows the generic error.
- `pages/reset-password` reads `?token`, takes the new password twice, and on success goes to sign-in
  with a "password changed" note. It shows a 400's message, and has a link to ask again.
- Locales: `auth.json`, vi and en.

**Bruno.** `auth/forgot password is 202 for anybody.yml` and `security-checks/reset with a bad token is 400.yml`.
*Correction (2026-09-27):* the second request was committed as `security-checks/reset with a made-up token is
400.yml`.

## Decisions

- **SHA-256, not bcrypt, for the token.** It is 256 bits of randomness and cannot be guessed, so a
  slow hash adds nothing, and a fast one lets a unique index find it.
- **Queue the email directly, not through `IEmailSender`.** The token would otherwise sit in an outbox
  message and a broker queue. The row is the one copy, and it is scrubbed after sending.
- **Deleting earlier unused tokens.** The newest link is the one the person just asked for. An older
  one still valid would be a second way in.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19 in the storefront

**Primary Dependencies**: `System.Security.Cryptography` (`RandomNumberGenerator`, `SHA256`), FluentValidation
12.1.1, MediatR 12.4.1, the email queue of specs/060; react-router, TanStack Query, react-i18next

**Storage**: New table `password_reset_tokens` in `ecommerce_identity_db` (5435), migration
`20260925035705_AddPasswordResetTokens`; rows in the existing `outgoing_emails`

**Testing**: `PasswordResetTests` (8, real PostgreSQL); Vitest for the two pages (9 new); Bruno; an end-to-end run
through Mailpit against the compose stack

**Target Platform**: Identity (5056) through the gateway (5000); the storefront

**Project Type**: New feature in Identity plus two storefront pages

**Performance Goals**: None

**Constraints**: #28 - the answer never reveals whether an address has an account; the token never crosses the
broker; one guarded claim decides a race

**Scale/Scope**: Two endpoints, one table, one email template, two pages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Everything stays in Identity: the account, the token, the email row. No other service is involved, and the broker is deliberately not used |
| **II. Clean Architecture Layering** | **Pass.** `PasswordResetToken` in Domain; commands, validators and `IPasswordResetRepository` in Application; the SQL claim in Infrastructure; the controller only dispatches |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The token, its email and its audit entry commit together. The claim is one guarded statement (`UPDATE ... WHERE "UsedAt" IS NULL AND "ExpiresAt" > now RETURNING`), so a second use affects zero rows; the password and the revocation share its transaction |
| **IV. Identity Comes From the Token** | **Pass.** The reset is anonymous by nature; the token is the identity, and it is single-use. No endpoint takes a user id |
| **V. Evidence Over Assumption** | **Pass.** Tests fail first, six mutation checks follow (four server, two client), and there is an end-to-end run through Mailpit whose output is in PR #144 |

**Post-design re-check**: no violations. Migration: a new table only; an earlier image ignores it.

## Constitution check

- III: the token, its email and its audit entry commit together. The claim is one guarded statement.
  Pass.
- IV: the reset is anonymous by nature; the token is the identity, and it is single-use. Pass.
- V: tests fail first, mutation checks follow, and there is an end-to-end run through Mailpit. Pass.
- Migration: a new table only.

(The lines above are the check as first written; the table extends it to all five principles.)

## Project Structure

### Documentation (this feature)

```text
specs/061-password-reset/
├── spec.md
├── plan.md                  # This file
├── research.md              # Six decisions
├── data-model.md            # password_reset_tokens; a link's life
├── quickstart.md
├── contracts/
│   └── http-api.md          # forgot-password, reset-password
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #144)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs          # EmailTemplate.PasswordReset
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/PasswordResetToken.cs
├── Ecommerce.Identity.Application/
│   ├── Auth/Commands/PasswordReset/PasswordReset.cs                      # commands, validators, handlers, ResetTokens
│   └── Email/{DispatchEmailsCommand,EmailTemplates}.cs                    # scrub; the template
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/PasswordResetTokenConfiguration.cs
│   ├── Migrations/20260925035705_AddPasswordResetTokens.cs
│   ├── Persistence/{ApplicationDbContext.cs,Repositories/PasswordResetRepository.cs}
│   └── DependencyInjection.cs
└── Ecommerce.Identity.WebApi/Controllers/AuthController.cs              # two endpoints, RequestLanguage()
server/tests/Ecommerce.Identity.Tests/{PasswordResetTests.cs,IdentityTestFixture.cs}
client/src/
├── pages/forgot-password/{index.tsx,index.test.tsx}
├── pages/reset-password/{index.tsx,index.test.tsx}
├── pages/sign-in/index.tsx                                               # "Forgot password?"
├── routes/index.tsx
├── services/auth/index.ts
└── locales/{en,vi}/auth.json
bruno/auth/forgot password is 202 for anybody.yml
bruno/security-checks/reset with a made-up token is 400.yml
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/auth/{db-design,security-best-practices}.md`,
`docs/features/email.md` (rule 7), `docs/overview/project-overview.md`, `docs/project/{backlog,decisions,timeline}.md`,
`docs/reference/{api,data-model}.md` (regenerated), `docs/testing/testing-strategy.md`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Nothing limits how often a link is asked for** - anybody can fill a stranger's inbox. That is #105,
  [specs/062](../062-auth-rate-limits/).
- A reset ends refresh tokens, but an **access token** already issued keeps working until it expires;
  [specs/065](../065-revoke-access-tokens/) closed that.
- For an unknown address the handler returns before any database work, so it does less than for a real one.
  Whether response timing could tell the two apart was not recorded as considered.
