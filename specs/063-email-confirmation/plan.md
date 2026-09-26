# Implementation Plan: Email confirmation

> Completed on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Branch**: `063-email-confirmation` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #106

## Summary

Registration stages a single-use, hashed, 24-hour link and an `EmailConfirmation` email in the account's own save;
an anonymous endpoint confirms with one guarded claim plus a guarded update of `users.EmailConfirmedAt`; a
signed-in endpoint sends another at most once a minute. Applying to sell requires a confirmed address and
approving waits for one. Existing accounts are backfilled as confirmed. The storefront draws a banner from
`emailConfirmed` on the auth response and gains a `/confirm-email` page. Decisions below and in
[research.md](research.md) (decision 47 in [docs/project/decisions.md](../../docs/project/decisions.md)).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19 in the storefront

**Primary Dependencies**: the hashing of specs/061 (`ResetTokens.NewToken`/`Hash`), the email queue of specs/060,
the gateway policies of specs/062; react-router, TanStack Query, react-i18next

**Storage**: `ecommerce_identity_db` (5435), migration `20260925060135_AddEmailConfirmation`: a nullable column
`users.EmailConfirmedAt` with a backfill, and a new table `email_confirmation_tokens`

**Testing**: `EmailConfirmationTests` (11) against real PostgreSQL, older Identity tests adapted; Vitest (10 new);
Bruno (the seller folder reads the link from Mailpit); an end-to-end run through the gateway and Mailpit

**Target Platform**: Identity (5056) through the gateway (5000); the storefront

**Project Type**: Feature in Identity, two gateway routes, storefront banner and page

**Performance Goals**: None

**Constraints**: the token never crosses the broker; buying stays open; an earlier image still reads `users`;
`AuthResponse` gains a field additively

**Scale/Scope**: Two endpoints, one column, one table, one template, a banner, a page

## Design

- **`users.EmailConfirmedAt`** (timestamptz, nullable). The migration adds it, then
  `UPDATE users SET "EmailConfirmedAt" = "CreatedAt"`. `DataInitializer` confirms the admin it seeds.
- **`email_confirmation_tokens`** has the same shape as `password_reset_tokens`: `Id`, `UserId`,
  `TokenHash char(64)` unique, `ExpiresAt`, `UsedAt`, `CreatedAt`, with a cascade from `users`.
- **`EmailConfirmations`** (Application, `Auth/Commands/EmailConfirmation/`):
  - `StageAsync(user, language)`. Deletes the account's unused links, then **stages** a new token and
    its `OutgoingEmail` through EF, so they commit with the caller's single save: registration's
    account, or the resend. `IOutgoingEmailRepository.Stage` adds the entity (`DataJson` is jsonb,
    written as a string). *Correction (2026-09-27):* as built, `StageAsync(user, language, now, ct)` does not
    delete anything; the resend handler calls `DeleteUnusedAsync` itself before staging, and a new account has
    no earlier link to delete. `StageAsync` also records `EmailConfirmationSent`.
  - `ConfirmEmailCommand(Token)`: in a transaction, `TryClaimAsync(hash, now)` returns the user id or
    null, then `TryConfirmAsync(userId, now)` runs the guarded UPDATE, then the audit entry.
  - `ResendConfirmationCommand(Language)`: signed in. Already confirmed → 409. Otherwise it locks the
    user's row and asks whether a token was made in the last minute (the pattern from specs/062). If so it
    returns silently; if not it stages a new link and saves.
- **Registration** (`RegisterCommandHandler`, `RegisterSellerCommandHandler`) calls `StageAsync` before
  its first save. The controller passes the request language, like forgot-password.
- **`AuthResponse`** gains `EmailConfirmed` (bool), filled by login, refresh and both registrations.
- **Shop applications:**
  - `EnsureMayApplyAsync` throws `ForbiddenException` with `code: EmailNotConfirmed` for an unconfirmed
    user.
  - Approval refuses an unconfirmed applicant with 409, before the guarded decision.
  - `ShopApplicationRow` and the staff response carry `ApplicantEmailConfirmed`.
- **Template** `EmailConfirmation` has vi and en words, a `/confirm-email?token=` link, and is in
  `ScrubbedOnceSent`.
- **Gateway:** `/api/auth/confirm-email` goes under `sign-in` (a guessable-token endpoint);
  `/api/auth/resend-confirmation` under `email`.
- **Storefront:**
  - `emailConfirmed` on the auth user, and `Auth.confirmEmail` / `Auth.resendConfirmation`;
  - a banner in the main layout while signed in and unconfirmed, with "Send it again";
  - a `/confirm-email` page that confirms and, when signed in, renews the session so the banner goes;
  - `/open-shop` says to confirm first instead of showing the form;
  - the admin shops page marks unconfirmed applicants.
- **Bruno:** the seller folder confirms the seller's address through Mailpit's API, the way a person
  would. Before that, approving the unconfirmed applicant is 409. Security checks: resend without a token
  is 401; confirming with a made-up token is 400.

## Decisions

1. **Existing accounts count as confirmed.** They predate the rule, and some already hold shops.
2. **Buying is not gated; selling is.** An unconfirmed address costs its owner nothing when paying. A shop
   is a public claim in the address's name.
3. **The approval is gated, not the seller registration.** Registering as a seller stays one step, and the
   application simply waits for the address, as the moderator also waits for it.
4. **24 hours, not 30 minutes.** A confirmation link grants nothing an attacker wants, and people open
   welcome emails late.

## Constitution check

- **I. Service Autonomy.** Everything is in Identity. Pass.
- **II. Clean Architecture.** Pass.
- **III. Atomic writes.** The token, the email and the account commit in one save, and a confirmation is
  guarded statements in one transaction. Pass.
- **IV. Identity from the token.** Resend reads the caller from `ICurrentUser`; confirm reads the account
  from the link's token, never from a body id. Pass.
- **V. Evidence.** Tests against real PostgreSQL, mutation checks, and an end-to-end run through Mailpit.
  Pass.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The list above is the check as first
written; this table states it in the standard form.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Everything is in Identity: the column, the tokens, the email row, the shop-application rules. No other service learns whether an address is confirmed |
| **II. Clean Architecture Layering** | **Pass.** `EmailConfirmationToken` and `User.EmailConfirmedAt` in Domain; `EmailConfirmations`, the handlers and `IEmailConfirmationRepository` in Application; the guarded SQL in `EmailConfirmationRepository` (Infrastructure); the controller only dispatches |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The token, the email and the account (or the resend) commit in one save; a confirmation is two guarded statements (`UsedAt IS NULL AND ExpiresAt > now`, then `EmailConfirmedAt IS NULL`) in one transaction; the resend interval is decided under a row lock. The token never enters an outbox |
| **IV. Identity Comes From the Token** | **Pass.** Resend reads the caller from `ICurrentUser`; confirm reads the account from the link's token, never from a body id. `emailConfirmed` on the response is for drawing; the server decides on its own |
| **V. Evidence Over Assumption** | **Pass.** Tests against real PostgreSQL, eleven mutation checks, the backfill verified on the real database (32 of 32 accounts), and an end-to-end run through Mailpit recorded in PR #146 |

**Post-design re-check**: no violations. The schema change is additive (a nullable column and a table).

## Project Structure

### Documentation (this feature)

```text
specs/063-email-confirmation/
├── spec.md
├── plan.md                  # This file
├── research.md              # Six decisions
├── data-model.md            # users.EmailConfirmedAt, email_confirmation_tokens, the backfill
├── quickstart.md
├── contracts/
│   └── http-api.md          # confirm-email, resend-confirmation, emailConfirmed, 403 / 409 on shops
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #146)

```text
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json               # two routes, sign-in / email policies
server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs            # EmailTemplate.EmailConfirmation
server/src/Services/Identity/
├── Ecommerce.Identity.Domain/Entities/{EmailConfirmationToken,User}.cs
├── Ecommerce.Identity.Application/
│   ├── Auth/Commands/EmailConfirmation/EmailConfirmation.cs               # commands, EmailConfirmations, handlers
│   ├── Auth/Commands/{Login,Refresh,Register,RegisterSeller}/...          # EmailConfirmed; Language; StageAsync
│   ├── Auth/Common/AuthResponse.cs                                         # EmailConfirmed
│   ├── Common/Interfaces/IShopApplicationRepository.cs
│   ├── Email/{EmailInterfaces,EmailTemplates}.cs                           # Stage(); the template; scrubbed
│   ├── ShopApplications/ShopApplicationFeatures.cs                         # 403 on apply, 409 on approve
│   └── DependencyInjection.cs
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/EmailConfirmationTokenConfiguration.cs
│   ├── Email/OutgoingEmailRepository.cs                                    # Stage
│   ├── Migrations/20260925060135_AddEmailConfirmation.cs
│   ├── Persistence/{ApplicationDbContext,DataInitializer}.cs
│   ├── Persistence/Repositories/{EmailConfirmationRepository,ShopApplicationRepository}.cs
│   └── DependencyInjection.cs
└── Ecommerce.Identity.WebApi/Controllers/AuthController.cs
server/tests/Ecommerce.Identity.Tests/{EmailConfirmationTests,AuditTests,EmailTests,IdentityTestFixture,ShopApplicationTests,SignInThrottleTests}.cs
client/src/
├── components/layout/confirm-email-banner/{index.tsx,index.test.tsx}
├── pages/confirm-email/{index.tsx,index.test.tsx}
├── pages/open-shop/, pages/admin-shops/                                     # guard; flag
├── layouts/main-layout/index.tsx, routes/index.tsx, context/auth/index.tsx
├── services/auth/{index.ts,types.ts}, services/shop-applications/types.ts
├── locales/{en,vi}/{auth,admin,seller}.json
└── test/render.tsx and three tests adapted to emailConfirmed
bruno/environments/local.yml                                                  # mailpitUrl
bruno/seller/{approving before the address is confirmed is 409,the seller confirms their email with the link}.yml (+4 renumbered)
bruno/security-checks/{confirming with a made-up token is 400,sending a confirmation link without a token is 401}.yml
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/auth/{db-design,security-best-practices}.md`,
`docs/features/{email,marketplace}.md`, `docs/overview/project-overview.md`, `docs/project/{backlog,decisions,timeline}.md`,
`docs/reference/{api,data-model,gateway}.md` (regenerated), `docs/testing/testing-strategy.md`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Changing an email address** is not possible at all, so there is nothing to re-confirm.
- An unconfirmed account can still buy and receive order emails at the address it typed (decision 2).
- The Bruno collection now needs Mailpit (`mailpitUrl`), so it cannot run against a stack without it.
- `local/seed-demo.py`, not committed, was updated so the demo still seeds (tasks.md T004); it is not part of the
  repository.
