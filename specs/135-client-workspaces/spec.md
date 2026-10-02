# Feature Specification: The client becomes a workspace of apps and packages

**Feature Branch**: `refactor/275-client-workspaces` | **Created**: 2026-10-02 | **Issue**: #275

**Status**: Draft

**Input**: Issue #275, "split the storefront into npm workspaces". This is the first step of moving the staff console
out of the shop into a **back office** of its own ([ADR-003](../../docs/architecture/adr-003-storefront-and-back-office.md),
#275-#280).

## Why

The staff console is about to leave the storefront for a second app (#276, #277). Both apps need the same UI kit, the
same HTTP client and session handling, the same services, hooks, translations and test helpers. Copying them into a
second app would leave two copies to drift apart: a fix in one, the defect in the other. This step reorganises the
client so that a second app can be added beside the first and share everything that is not a page. **Nothing a person
sees changes.**

## User Scenarios & Testing *(mandatory)*

### US1 - The storefront is the same storefront (Priority: P1)

A shopper, seller or member of staff uses the storefront exactly as before.

**Acceptance Scenarios**:

1. **Given** the reorganised client, **When** it is built, tested and run, **Then** every unit test that passed before passes (693), the browser flows pass, and the image serves the storefront as `verify-storefront-image.sh` checks.
2. **Given** the storefront in a browser, **Then** no page looks different. In particular the UI kit's styles are still generated, even though its source no longer lives inside the app.

---

### US2 - A second app can share everything but pages (Priority: P1)

A developer adding the back office (#276) imports the UI kit and the core from packages, rather than copying them.

**Acceptance Scenarios**:

1. **Given** `packages/ui` and `packages/core`, **Then** neither imports anything from an app. A test fails if one does.
2. **Given** an app, **Then** `@/` means that app's own folders only. Shared code is imported by package name: `@ecommerce/core/...`, `@ecommerce/ui/...`.
3. **Given** `shadcn add` run in `packages/ui`, **Then** the component lands in the kit and needs no editing, as before.

### Edge Cases

- Tests that read files from `server/` (notification kinds, gateway routes, audit actions) find them wherever they now live and whichever directory the tests are started from.
- The three tests in shared folders that exercised app components move to the app.
- One stray screenshot committed at `client/undefined/` goes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `client/` is an npm workspace with `apps/storefront`, `packages/ui` and `packages/core`, and one lock file.
- **FR-002**: `packages/ui` holds the shadcn kit (with `cn`). `packages/core` holds `config`, `context`, `services`, `hooks`, `utils`, `constants`, `locales` and the test helpers. The app keeps `components`, `pages`, `layouts` and `routes`.
- **FR-003**: `npm run lint`, `npm test`, `npm run build`, `npm run dev` and `npm run e2e` keep working from `client/`, so CI changes as little as possible.
- **FR-004**: The client README, the architecture page and CLAUDE.md describe the new layout.

## Success Criteria *(mandatory)*

- **SC-001**: The same 693 unit tests pass, the browser flows pass, and the image check passes.
- **SC-002**: No import from a package reaches into an app (checked by a test).

## Assumptions

- No package needs a build step of its own. The packages are source, resolved by alias like `@/` is today, and bundled by the app that imports them.
