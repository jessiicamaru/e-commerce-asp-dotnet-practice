# Tasks: Unlock rules

- [X] T001 [US1] Tests first in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`: self, moderator-on-moderator, a long lock for a moderator, allowed cases, nothing recorded on refusal
- [X] T002 [US1] `ModerationRules.EnsureMayRelease` + call it in `Handle(UnlockUserCommand)` in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- [X] T003 [US2] Test then disable "Unlock" in `client/src/pages/admin-users/index.test.tsx` / `index.tsx`
- [X] T004 Mutation checks; docs `docs/features/moderation-and-staff.md`, `docs/project/*`, CLAUDE.md
