# Feature Specification: A back office for staff, with its own sign-in

**Feature Branch**: `feat/276-back-office-app` | **Created**: 2026-10-02 | **Issue**: #276

**Status**: Draft

**Input**: Issue #276, "a back-office app for staff, with its own sign-in". This is the second step of
[ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md). It builds on specs/135, which made the client
a workspace.

## Why

Administrators and moderators are to work in a **back office** of their own, on its own origin, rather than inside the
storefront. Before the console's pages can move there (#277), the back office has to exist:

- an app that is built and runs, locally and as an image;
- that staff can sign in to, with their password and then the code from their authenticator;
- whose session is its own and never mixed with the storefront's.

## User Scenarios & Testing *(mandatory)*

### US1 - Staff sign in to the back office (Priority: P1)

An administrator or moderator opens the back office, signs in with email, password and the code, and sees a page that
greets them by name. They can sign out.

**Acceptance Scenarios**:

1. **Given** a staff member with two-factor sign-in set up, **When** they enter the right password and code, **Then** they see the back office's home with their name.
2. **Given** a wrong password, a wrong code, or a locked account, **Then** the refusal reads exactly as it does on the storefront: the same form and the same words.
3. **Given** a signed-in staff member, **When** they reload the page, **Then** they are still signed in (the back office's own refresh cookie). **When** they sign out, **Then** a reload asks them to sign in again.

---

### US2 - The back office is for staff only (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a customer or seller signs in, **Then** the back office says it is for staff, offers to sign out, and shows nothing else.
2. **Given** a staff member who has not set up two-factor sign-in, **Then** the back office says so and where to do it (their account on the storefront). Until then the server gives them no staff role (specs/110).
3. **Given** nobody signed in, **When** any back-office address is opened, **Then** they are sent to sign in and come back afterwards.

---

### US3 - The back office's session is its own (Priority: P1)

**Acceptance Scenarios**:

1. **Given** the storefront and the back office open in one browser, **When** a person signs in to one, **Then** the other is not signed in by it, and signing out of one does not sign them out of the other. In development the apps run on `localhost` and `portal.localhost`: cookies ignore the port, so two ports on one host would share the refresh cookie.

---

### US4 - It ships like the storefront (Priority: P2)

**Acceptance Scenarios**:

1. **Given** a merge, **Then** the back office is published as `ecommerce-back-office` with the other images. It is checked by the same image test and scanned for secrets.
2. **Given** compose, **Then** the back office runs on `:8089`. The gateway believes its `X-Forwarded-For`, so sign-in rate limits count each person, not the back office's nginx.

### Edge Cases

- A sign-in on the back office that needs a code uses the same challenge as the storefront (specs/110): a dead challenge restarts from the password.
- The storefront's sign-in page keeps its own extras: why the person was sent there, forgot-password and create-an-account. The back office's has none of them. Staff accounts are not made by sign-up, and a forgotten password is reset from the storefront.
- Staff roles still reach storefront sessions until #278. This step adds an app; it does not yet take anything away from the storefront.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `apps/back-office`: Vite on `portal.localhost:5174`, proxying `/api` to the gateway. Routes: `/sign-in`, and a guarded `/` that greets the staff member.
- **FR-002**: The two-step sign-in form is one component in `packages/core`, used by both apps. The storefront's page wraps it with its own extras.
- **FR-003**: The theme (colours, fonts, radii) moves to `packages/ui/src/theme.css`, imported by both apps.
- **FR-004**: One `client/Dockerfile` builds either app (`APP`, default `storefront`). Compose runs `back-office` on `:8089` at a fixed address the gateway trusts.
- **FR-005**: CI builds, checks and scans the back-office image, publishes it as the 11th image, and keeps it in the prune list. Playwright signs a moderator in to the back office.
- **FR-006**: Docs: ADR-003's progress, client README, architecture, running-in-containers, CLAUDE.md.

## Success Criteria *(mandatory)*

- **SC-001**: A moderator signs in to the back office in a real browser with password and code, and sees their name (Playwright, against compose).
- **SC-002**: No storefront test changes its expectations (the shared form keeps every word and every behaviour).

## Assumptions

- Two-factor enrolment stays on the storefront for now. The back office says where to do it rather than duplicate it.
- The back office has no pages of its own yet beyond the greeting; #277 moves the console.
