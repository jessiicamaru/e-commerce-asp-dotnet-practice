# Feature Specification: Sign Up, Sign In, Stay Signed In

**Feature Branch**: `015-storefront-auth` · **Created**: 2026-09-22 · **Status**: Implemented

**Input**: Issue [#35](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/35), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

The token story - short access token in the body, refresh token in an HttpOnly cookie - was argued
for in `docs/features/auth/security-best-practices.md` and had never met a browser.

## What building it found

**There was no way to sign out.** The refresh token is an HttpOnly cookie no script can delete, so a
client that only forgets its access token is signed straight back in by the next silent refresh.
`POST /api/auth/logout` now deletes the refresh token server-side and clears the cookie. That is a
backend change, made here because the feature cannot be correct without it.

**Registration validates nothing** - `not-an-email`, a one-character password and empty names create
an account. Filed as [#43](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/43)
rather than worked around in the client.

## Requirements

- **FR-001**: A person can create an account and is signed in.
- **FR-002**: A person can sign in; a wrong password shows one message whether or not the email exists.
- **FR-003**: The access token lives in memory only; a reload restores the session through the refresh cookie.
- **FR-004**: A 401 on any request triggers one silent refresh and one retry; concurrent 401s share it.
- **FR-005**: Signing out ends the session on the server; a refresh afterwards is 401.
- **FR-006**: Pages that need a customer wait for the session to be restored before redirecting.
