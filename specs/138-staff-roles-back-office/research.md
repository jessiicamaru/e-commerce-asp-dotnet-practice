# Research: Staff roles only in a back-office session

## D1 - The app comes from `Origin`, stored with the session

**Decision**: When a session is created, Identity reads the request's `Origin`. It is a back-office session if that
origin is one of `BackOffice:Origins`, a storefront session otherwise. The answer is stored on the refresh token and
carried across every rotation.

**Rationale**: A browser writes `Origin` itself on every `POST`, and a script cannot change it (a forbidden header). So
a script running on the storefront cannot ask for a back-office session. The back office's refresh cookie is host-only
on `portal.*`, so the storefront's scripts cannot present it either.

**Alternatives rejected**:
- A field in the body: whatever runs on the storefront could set it.
- `Referer`: optional, and stripped by referrer policies.
- Deciding at every refresh from that request's `Origin`: it changes nothing a browser can do, and it adds a second
  place that decides.

## D2 - Old sessions are storefront sessions

**Decision**: The column is nullable, and null means `Storefront`.

**Rationale**: Expand-only (constitution, specs/006). Treating unknown as the less privileged app costs a staff member
one sign-in to the back office. Guessing the other way would keep exactly the sessions this change exists to end.

## D3 - The storefront still knows who is staff, to draw

**Decision**: `AuthResponse.StaffAccount` says whether the account holds a staff role, whatever this session carries.
The storefront draws "Management platform" from it.

**Rationale**: After this change `roles` on a storefront session never contains a staff role, so the storefront would
stop offering the back office to the very people it is for. The flag grants nothing: every staff endpoint still
decides from the token's roles.

## D4 - Tools sign in as the back office

**Decision**: Every tool that acts as staff - the verify scripts, Bruno, the seed scripts, Playwright's API helper -
sends `Origin: <back office>` on the step that creates the session.

**Rationale**: They act as staff, which is what the back office is for. A tool that is not a browser can send any
`Origin`; it still has to know the password and the code.
