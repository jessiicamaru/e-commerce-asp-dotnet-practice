# Research: A back office for staff, with its own sign-in

## D1 - A different host in development, not only a different port

**Decision**: The back office runs on `portal.localhost:5174` in development, and Playwright opens it at
`portal.localhost:8089` against compose.

**Rationale**: Cookies are scoped by host, not port (RFC 6265). On `localhost:5173` and `localhost:5174` the two apps'
`refreshToken` cookies would be one cookie: signing in to one would sign the other out. Browsers resolve `*.localhost`
to the loopback address and treat it as a secure context, so Identity's `Secure` cookie is still accepted over HTTP.

**Alternatives rejected**:
- A different cookie name per app: needs an Identity change and still shares every other cookie.
- Ports only: shares the session, which is exactly what ADR-003 forbids.

## D2 - Shared components live in `packages/core/src/components`

**Decision**: The sign-in form and the query-state messages (`ErrorMessage`, `LoadingRows`...) move to
`packages/core/src/components`.

**Rationale**: They need the core: `useAuth`, the API errors and the translations. The kit (`packages/ui`) must not
import the core (specs/135), so they cannot go there. A third package for two components would be structure without a
reason.

**Alternatives rejected**:
- Copying them into the back office: two forms whose refusals drift apart.
- A `packages/components` package now: premature. #277 will show which components are really shared.

## D3 - One sign-in form with slots, not two pages

**Decision**: `SignInForm` holds both steps and every refusal. It takes `onSignedIn`, `onSetupRequired`, an optional
description and a footer. The storefront passes its reason, forgot-password and sign-up links; the back office passes
nothing.

**Rationale**: The two-step logic (challenge, recovery code, a dead challenge restarting) is the part that can be
wrong, and it is tested once.

## D4 - One Dockerfile, an `APP` argument

**Decision**: `client/Dockerfile` takes `ARG APP=storefront` and builds `apps/$APP`. The nginx template is the same for
both.

**Rationale**: The two images differ only in which bundle they serve. The default keeps every existing
`docker build client` working unchanged.

**Alternatives rejected**: A second Dockerfile, which would mean two copies of the workspace install steps.

## D5 - The back office says where to set up two-factor, rather than doing it

**Decision**: A staff member whose sign-in answers `SetupRequired` is told to set two-factor sign-in up from their
account on the storefront.

**Rationale**: Enrolment already exists, with its tests, on the storefront. Until #278 that is also where the person
has a session to do it from.

## D6 - The image check is shared

**Decision**: `verify-storefront-image.sh` takes the app's name as an optional second argument and checks the back
office the same way. The checks are not specific to the shop: the app is served, a deep link works, `/api` is
forwarded, and the cache headers are right.
