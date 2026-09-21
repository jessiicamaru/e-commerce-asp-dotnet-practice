# Tasks: Sign Up, Sign In, Stay Signed In

- [X] T001 Identity: `POST /api/auth/logout` - delete the refresh token, clear the cookie, always 204
- [X] T002 Identity.Tests `SessionTests`: refresh works before logout, 401 after; logout without a session is a quiet no-op
- [X] T003 Client `AuthProvider`: token in memory, restore on load, single shared refresh, sign in / up / out
- [X] T004 Sign-in and sign-up pages; `RequireAuth`; top bar shows who is signed in
- [X] T005 Bruno: `logout` and `refresh after logout is 401`
- [X] T006 File what the backend lacks: registration validation (#43)
- [ ] T007 PR `Closes #35`; CI green; squash-merge

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
