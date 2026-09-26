# Tasks: Sign Up, Sign In, Stay Signed In

> Completed on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull
> request and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `015-storefront-auth`

**Tests**: Included for the backend change (`SessionTests`, against a real PostgreSQL) and in Bruno. No
client tests - the client had none until specs/028.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (sign up), US2 (sign in), US3 (stay signed in), US4 (sign out)

T001-T007 are the list written at the merge, kept as they were.

- [X] T001 Identity: `POST /api/auth/logout` - delete the refresh token, clear the cookie, always 204
- [X] T002 Identity.Tests `SessionTests`: refresh works before logout, 401 after; logout without a session is a quiet no-op
- [X] T003 Client `AuthProvider`: token in memory, restore on load, single shared refresh, sign in / up / out
- [X] T004 Sign-in and sign-up pages; `RequireAuth`; top bar shows who is signed in
- [X] T005 Bruno: `logout` and `refresh after logout is 401`
- [X] T006 File what the backend lacks: registration validation (#43)

## Recorded after the merge

Added on 2026-09-27 from the diff of #44.

- [X] T008 [US4] `LogoutCommand` (`string? RefreshToken`) and `LogoutCommandHandler` in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Logout/`, returning early for a missing or unknown token and removing the row otherwise, with one `SaveChangesAsync`
- [X] T009 [US3] `client/src/auth/useAuth.ts`: `User`, `AuthResponse`, `AuthState` and `useAuth()`, which throws outside the provider
- [X] T010 [US3] Wire the provider into the call layer with `configureAuth(() => accessToken, refresh)` in `client/src/auth/AuthContext.tsx`
- [X] T011 [US2] Return a signed-in person to `state.from` after sign-in in `client/src/pages/SignInPage.tsx`, which `RequireAuth` sets when it redirects
- [X] T012 [US1] Show Identity's field errors (400) and the duplicate-email refusal (409) in `client/src/pages/SignUpPage.tsx`
- [X] T013 [P] Update `docs/features/auth/jwt-setup.md` and `docs/features/auth/security-best-practices.md` for logout
- [X] T007 PR [#44](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/44) `Closes #35`; CI green; squash-merged as `227bf13` on 2026-09-22

## Dependencies & Execution Order

T008 → T001 → T002, T005 (the endpoint before its tests). On the client, T009 → T003 → T010 → T004,
T011, T012. The two sides are independent until the end-to-end check.

## What actually happened

Verified through the Vite proxy (the path a browser takes), with a cookie jar:

```text
register via proxy     -> 200   set-cookie: refreshToken=...; path=/; secure; samesite=lax; httponly
refresh (a reload)     -> 200
logout                 -> 204   set-cookie: refreshToken=; expires=1970 ...
refresh after logout   -> 401
```

Identity.Tests 24/24; Bruno 53/53. **Not verified in a real browser** - the UI was type-checked and
built, and the HTTP behaviour it relies on was exercised with curl; nobody has clicked through it.

## Notes

- **T007 is listed last although its id is lower**: it was the last task at the merge; T008-T013
  describe work already inside that pull request.
- 13 tasks, all done.
