# Tasks: Email confirmation

- [ ] T001 [US1] [US2] [US3] [US4] Tests first in `server/tests/Ecommerce.Identity.Tests/EmailConfirmationTests.cs`. Cover:
  - registering (both kinds) queues one confirmation email, and the account is unconfirmed;
  - the link confirms once, and a used, expired or unknown token is 400;
  - two submissions at once confirm once;
  - resend replaces the link, sends at most once a minute, and an already confirmed account is 409;
  - sign-in and refresh report `EmailConfirmed`;
  - an unconfirmed customer cannot apply to sell (403 `EmailNotConfirmed`);
  - approving an unconfirmed applicant is 409, and staff see the flag;
  - audit entries hold no token, and the sent row is scrubbed.
- [ ] T002 [US1] [US2] [US3] [US4] Identity:
  - `EmailConfirmedAt`, the token entity, configuration and migration (with the backfill);
  - `DataInitializer`, the repository and `EmailConfirmations`;
  - both registrations, `AuthResponse`, the shop application rules, the template;
  - the controller endpoints, and the gateway routes.
- [ ] T003 [US1] [US2] [US3] Storefront tests first, then:
  - the service, the user type, the banner, the `/confirm-email` page;
  - the `/open-shop` guard, the admin shops flag, and the words.
- [ ] T004 Bruno:
  - the seller folder confirms through Mailpit, after "approving an unconfirmed applicant is 409";
  - security checks.

  Then end to end through Mailpit. Update `local/seed-demo.py`, which is not committed, so the demo still seeds.
- [ ] T005 Mutation checks. Docs:
  - `features/auth/*`, `features/email.md`, `features/marketplace.md`;
  - the reference, regenerated;
  - timeline, backlog, decisions and counts;
  - CLAUDE.md.
