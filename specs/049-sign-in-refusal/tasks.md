# Tasks: Sign-in refusal

- [X] T001 [US2] `ForbiddenException` carries optional facts in `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/ForbiddenException.cs`; `GlobalExceptionHandler` writes them as extensions, reserved keys winning
- [X] T002 [US2] Handler test in `server/tests/Ecommerce.Identity.Tests/ForbiddenProblemTests.cs`: facts reach the 403 body in Production; `traceId` cannot be overwritten
- [X] T003 [US2] `LoginCommandHandler` refuses with `code`, `until`, `reason`; assert them in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`
- [X] T004 [US2] Bruno `bruno/admin-users/the locked customer cannot sign in.yml` asserts `code`, `until`, `reason`
- [X] T005 [US1] Client tests first in `client/src/pages/sign-in/index.test.tsx`: locked, banned (both languages), wrong password, other 403, no body
- [X] T006 [US1] `client/src/pages/sign-in/refusal.ts` + `index.tsx` + `client/src/locales/{en,vi}/auth.json`
- [ ] T007 Mutation checks, then docs: `docs/features/moderation-and-staff.md`, `docs/architecture/error-handling-and-shared-building-block.md`, `docs/project/timeline.md`, `docs/project/backlog.md`
