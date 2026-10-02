# ADR 003: A Storefront and a Back Office, Two Apps in One Monorepo

* **Status**: Accepted
* **Deciders**: the project owner, with the recommended options
* **Date**: 2026-10-02
* **Issues**: #275-#280; first step [specs/135-client-workspaces](../../specs/135-client-workspaces/)

---

## 1. Context

Until now the staff console - everything administrators and moderators do, from approving products to recording
payouts - lived inside the shop, under `/admin`. One React app served shoppers, sellers and staff, and a staff member
was signed in to it with the same session whether they were buying a camera or banning an account.

Two things make that the wrong shape:

- **Staff do not shop.** Their work has its own navigation, its own pages and its own pace. Carrying it inside the shop
  makes both worse: the shop's header grows a staff icon, and the console inherits the shop's frame.
- **The shop is where untrusted content lives.** Reviews, questions, shop descriptions and product text are written by
  the public. If any of it ever slipped past sanitising, a script running on the shop's origin could act with whatever
  session that origin holds. Today that includes an administrator's. Security practice keeps a privileged session on
  an origin of its own; this is called *origin isolation*, or *privilege separation*.

## 2. Decision

**The shop becomes the *storefront* and the staff console becomes the *back office*. They are two applications, each
on its own origin (`ecommerce.com` and `portal.ecommerce.com`), built from one monorepo.**

- **Two SPAs, linked, not composed.** Each has its own bundle, routes, nginx and sign-in. The storefront's account
  page links to the back office; neither renders inside the other.
- **Shared code is shared at build time.** `client/` becomes an npm workspace: `apps/storefront`,
  `apps/back-office`, `packages/ui` (the component kit) and `packages/core` (HTTP client, session, services, hooks,
  translations, test helpers). An app imports a package like a library.
- **Each app has its own session.** Each nginx forwards `/api` to the gateway, so each app is one origin with no CORS,
  and its refresh cookie belongs to its own host. A staff member signs in to the back office separately: password,
  then the TOTP code.
- **Staff roles only in a back-office session** (#278). On the storefront a staff member is a customer.
- **Sellers stay in the storefront** for now. A separate seller centre could follow the same pattern later.

Delivered in order: workspaces (#275), the back-office app and its sign-in (#276), moving the console (#277), then
staff roles confined to back-office sessions (#278).

## 3. Why not call it a micro-frontend

"Micro-frontend" usually means one product, often one page, assembled **at run time** from pieces that different teams
build and deploy independently. A shell loads the pieces through Module Federation, single-spa, web components or
iframes. That buys team autonomy, and it costs:

- a shell;
- one shared React instance whose version every piece must match;
- coordinated routing and state;
- styling that must not collide;
- versioned contracts between pieces.

None of that is wanted here. There is one team and one repository, the two apps are deployed together, and the only
goal is separating audiences and sessions.

Some authors would count a split by sub-domain as a *vertical-split* micro-frontend. By that broad definition this
qualifies, without a shell. The word is avoided anyway, because it points the next developer at Module Federation, and
its costs would buy nothing. If one day a piece must be composed into another app at run time, built and deployed
separately, that is the moment for a micro-frontend. The packages introduced here do not stand in its way.

## 4. Alternatives rejected

| Alternative | Why not |
| :-- | :-- |
| Keep `/admin` inside the shop | The two problems in section 1 remain. |
| A copy of the shop's code as a second app | Two copies of the kit, the HTTP client and the services drift apart. |
| Module Federation (a shell loading remote modules) | The costs of section 3, for no benefit at one team. |
| Passing the access token to the back office in the URL | Navigation cannot carry an `Authorization` header. A token in a URL lands in history, server logs and `Referer`. |
| One refresh cookie for both apps (`Domain=.ecommerce.com`) | A script on the storefront could then use a staff session: the problem in section 1. |
| A one-time hand-off code from storefront to back office | Sound, and it saves typing the password. Deferred as #279 until signing in twice proves a nuisance. |
| A separate token audience for the back office | A stricter second line, enforced by every service. Deferred as #280. |

## 5. Consequences

- **Positive**:
  - A staff session is never on the origin that shows public content.
  - Each app's bundle carries only its own pages.
  - The console can get its own navigation and look.
  - The client's layering becomes explicit: packages never import an app, and a test checks it.
- **Negative**:
  - Staff sign in twice: once on the storefront, once in the back office.
  - One more image to build, publish and run.
  - One more origin to configure: the gateway's trusted proxies, and the cookie host.
- **Watch for**:
  - In development the two apps must be different **hosts** (`localhost` and `portal.localhost`), not different
    ports. Cookies ignore the port, so on one host the apps' refresh cookies would overwrite each other.
  - A notice sent to staff must link into the back office, not to a storefront page that no longer exists.

## 6. Progress

| Step | Issue | Record | State |
| :-- | :-- | :-- | :-- |
| The client becomes a workspace: `apps/storefront`, `packages/ui`, `packages/core` | #275 | [specs/135](../../specs/135-client-workspaces/) | Done |
| The back office exists: sign-in with a code, a staff guard, its own image on `:8089` | #276 | [specs/136](../../specs/136-back-office-app/) | Done - [back office](back-office.md) |
| The console's pages move to the back office | #277 | - | Next |
| Staff roles only in a back-office session | #278 | - | After #277 |
| A one-time hand-off code; a separate token audience | #279, #280 | - | Deferred |
