# Tasks: Password reset

- [X] T001 [US1] [US2] [US3] Tests first in `server/tests/Ecommerce.Identity.Tests/PasswordResetTests.cs`: the same answer for a known and an unknown address; a hashed token and a queued email with the link; asking again replaces the link; a reset changes the password and ends every session; used, expired and unknown tokens are 400; a concurrent use of one token; no secret in the audit entries or in the sent row
- [X] T002 [US1] [US2] Identity: `Domain/Entities/PasswordResetToken.cs`, configuration + migration, repository, `Application/Auth/Commands/PasswordReset/*`, the `PasswordReset` template, the scrub in `DispatchEmailsCommand`, endpoints in `AuthController`
- [X] T003 [US1] [US2] Storefront tests first, then `pages/forgot-password`, `pages/reset-password`, the sign-in link, `services/auth`, routes, locales
- [X] T004 Bruno requests; end to end through Mailpit
- [X] T005 Mutation checks; docs `docs/features/auth/*`, `docs/features/email.md`, `docs/reference` regenerated, `docs/project/*`, CLAUDE.md
