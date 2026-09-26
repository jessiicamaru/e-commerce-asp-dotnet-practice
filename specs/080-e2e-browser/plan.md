# Implementation Plan: The storefront in a real browser

> Completed on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Branch**: `080-e2e-browser` | **Spec**: [spec.md](spec.md) | **Issue**: #117

**Merged**: PR #164, 2026-09-26 | **Research**: [research.md](research.md) | **Data**: [data-model.md](data-model.md) |
**Contracts**: [contracts/README.md](contracts/README.md) | **Validation**: [quickstart.md](quickstart.md)

## Summary

Add Playwright to `client/` and four flows that click through the real storefront against the real stack - a
moderator approves a product, a customer buys and it settles to *Paid*, the seller ships, the customer confirms and
reviews. Nothing is mocked: the browser talks to the storefront container on `:8088`, whose nginx forwards `/api` to
the gateway. The data each flow starts from is made through the same API, fresh for every run, and the products and
category are removed at the end.

Locally the browser is the Edge that Windows already has (`channel: 'msedge'`), so nothing is downloaded; in CI it
is Playwright's Chromium, in a new `browser-e2e` job after the saga job that builds and starts the whole stack with
compose, uploads traces and logs on failure, and that `publish` now waits for.

The first run found a real defect - a toast lost whenever saving remounts its form - fixed in three components with
`mutateAsync().then(...)`, each with a unit test.

## Technical Context

**Language/Version**: TypeScript `~6.0.2` (the storefront's), Node 22 in CI

**Primary Dependencies**: `@playwright/test` `^1.63.0` (new dev dependency); the storefront's React 19, TanStack
Query 5 and sonner for the defect fix. No server package changed

**Storage**: None of its own. Test data goes into each service's own PostgreSQL through the gateway (see
[data-model.md](data-model.md))

**Testing**: Playwright (`npm run e2e`) for the flows; Vitest + Testing Library for the three regression tests of the
defect; the negative control of a renamed gateway route, by hand

**Target Platform**: Microsoft Edge on the developer's Windows machine; Playwright's Chromium on `ubuntu-latest` in
GitHub Actions. Desktop viewport (`devices['Desktop Chrome']`), locale `en-US`

**Project Type**: Web front end - a test suite beside the storefront, plus a CI job

**Performance Goals**: None set. The CI job took 5m14s on the pull request's run, stack build included; its limit
is `timeout-minutes: 40`, with 600 seconds for the stack to become healthy

**Constraints**: Nothing mocked (FR-006). Nothing downloaded on the developer's machine (FR-003). One worker, the
flows in order (FR-007). Setup through the API, never SQL (FR-008). Every asynchronous wait bounded (FR-009). A
test's limit is 90 seconds and an assertion's 20, except the ones research D8 lists

**Scale/Scope**: Four flows, three people and two products per run. Five files added under `client/`, three
components and their tests changed, one CI job

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. This section was written on
2026-09-27, after the merge; the plan as merged had none.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service, table or contract changed. The suite makes its data through each owning service's own API via the gateway - never SQL into anybody's database - and where it waits on a copy (Catalog's `availability` read model) it only observes it; nothing in the suite or the storefront decides a sale from it |
| **II. Clean Architecture Layering** | **Pass.** No server code changed. On the client, the suite is its own TypeScript project outside `src/` (`tsconfig.e2e.json`), and the defect fix stays inside the three components that show the toast; the hooks and services under them are untouched |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not engaged.** No handler, consumer or outbox changed. The flows run through the paths the principle governs - the saga, `ParcelDeliveredEvent`, Catalog's `ProductCreated` reaching Inventory - over the real broker, and wait for their effects with bounded polls instead of assuming an order between messages (research D8) |
| **IV. Identity Comes From the Token** | **Pass.** Every setup request carries a real token from `POST /api/auth/login`; roles come from the real endpoints (a shop application approved by the administrator, Moderator granted through `PUT /api/users/{id}/roles/Moderator`), never from a forged token or a database edit. The administrator's credentials come from `ADMIN_EMAIL` / `ADMIN_PASSWORD` and are never written into the suite; CI uses throwaway values that die with the runner |
| **V. Evidence Over Assumption** | **Pass, and the reason the feature exists.** The check exercises the real dependency for the property it claims - that pages reach the API - by driving a real browser against the real stack. It was shown able to fail: a renamed cart route made the buying flow fail. The defect fix carries tests that unmount before the save answers, and the pull request records that reverting either editor's fix turned its test red. What is not verified is named in "What this feature does not finish" |

**Post-Phase 1 re-check**: no violations. The design added no service, table, message or route, so nothing new came
into any principle's reach. The Complexity Tracking table below is empty.

## Project Structure

### Documentation (this feature)

```text
specs/080-e2e-browser/
├── spec.md              # Four flows, four user stories, FR-001..FR-014, SC-001..SC-006
├── plan.md              # This file
├── research.md          # D1-D13: Playwright, the browsers, compose, API setup, waits, the lost toast
├── data-model.md        # No table changed; the test data a run makes and removes
├── contracts/
│   └── README.md        # No interface changed; the routes and endpoints the flows rely on
├── quickstart.md        # Eight validation scenarios, including the negative control
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # T001-T006 as merged, expanded to the full list
```

### Source Code (repository root)

```text
client/
├── playwright.config.ts                # NEW - one project, one worker, Edge unless E2E_BROWSER_CHANNEL says otherwise
├── tsconfig.e2e.json                   # NEW - the suite as its own TypeScript project
├── tsconfig.json                       # references tsconfig.e2e.json, so tsc -b checks e2e/
├── package.json, package-lock.json     # @playwright/test; "e2e": "playwright test"
├── .gitignore                          # test-results/, playwright-report/, blob-report/
├── e2e/
│   ├── flows.spec.ts                   # NEW - the four flows in one serial describe
│   └── support/
│       ├── api.ts                      # NEW - Api: people, products, stock, address, Mailpit, cleanUp
│       └── ui.ts                       # NEW - signIn (English), findOnPages
├── src/components/product/product-reviews/
│   ├── index.tsx                       # ReviewForm: mutateAsync().then
│   └── index.test.tsx                  # the form gone before the save answers
├── src/pages/admin-emails/
│   ├── email-editor.tsx                # save, reset: mutateAsync().then
│   ├── email-versions.tsx              # restore: mutateAsync().then
│   └── index.test.tsx                  # the editor gone before the save answers
└── src/pages/admin-wording/
    ├── wording-editor.tsx              # save, reset, restore: mutateAsync().then
    └── index.test.tsx                  # the editor gone before the save answers

.github/workflows/ci.yml                # NEW job browser-e2e; publish needs it
```

Documentation touched in the same pull request: `docs/testing/testing-strategy.md` (a *Browser end to end* row and
section), `client/README.md`, `CLAUDE.md`, `docs/overview/project-overview.md` (counts),
`docs/project/timeline.md` and `docs/project/backlog.md` (#117 fixed; the backlog empty).

**Structure Decision**: The suite lives in `client/`, beside the pages it drives, because it is the storefront's
test and uses the storefront's `package.json` and `node_modules`; it sits in `e2e/` rather than `src/` so Vitest
never reads it and the browser bundle never includes it. `support/` holds the two helpers so the spec file reads as
the four flows and nothing else.

## Design

- `client/playwright.config.ts`:
  - one project with one worker, running the flows in order;
  - the base URL is `E2E_BASE_URL` (default `http://localhost:8088`) and the locale is en-US;
  - traces and screenshots are kept only on failure;
  - the browser is `E2E_BROWSER_CHANNEL`, default `msedge`, and empty means Playwright's Chromium.
- `client/e2e/support/api.ts` (`Api`) holds the setup, through the gateway as a person's clicks go:
  - people: a customer, a seller confirmed through Mailpit and approved, and a moderator granted by the
    administrator;
  - products: a category, a seller's product listed, approved and stocked (waiting for the catalogue to say
    *InStock*), and an address;
  - `cleanUp` at the end.
- `client/e2e/support/ui.ts`:
  - `signIn` goes through the sign-in page, in English;
  - `findOnPages` turns the pages of a paged list, giving each page a few seconds, because the counter changes at
    the click while the rows arrive with the next response.
- `client/e2e/flows.spec.ts` is one serial `describe` holding the four flows. The order id is carried from the
  purchase to the shipping and the review.
- `client/tsconfig.e2e.json` is referenced from `tsconfig.json`, so `tsc -b` checks the suite. Vitest still only
  reads `src/`.

Also in the design, from the code at the merge:

- The CI job `browser-e2e` needs `saga-e2e` and `client`, writes a throwaway `server/.env` (including
  `PAYMENT_OUTCOME=Approve` and `STOREFRONT_URL=http://localhost:8088`, which Identity puts in the confirmation
  link), starts the stack with `docker compose ... up -d --build --wait --wait-timeout 600`, installs Node 22, the
  storefront's packages and Chromium, and runs `npx playwright test` with `E2E_BROWSER_CHANNEL: ''`. On failure it
  writes every container's log to `client/test-results/compose.log` and uploads `test-results/` and
  `playwright-report/` as `browser-e2e-traces` for 7 days.
- In CI the config forbids `.only` and keeps retries at 0, and adds an HTML report to the `list` reporter.

## Found on the way

The first run found a real defect. **A toast lost when saving remounts its form:**
- the first review gives `ReviewForm` a new key;
- a save, reset or restore gives the email and notice editors a new version.

Each remounts before a callback handed to `mutate()` runs, and TanStack does not run those callbacks for a component
that is gone. The change was saved, but nobody was told. The fix is `mutateAsync().then(...)`.

Each fix has a unit test that unmounts before the save answers, and each test fails when its fix is reverted.

> Clarified on 2026-09-27, from the code at the merge: there are **three** such tests, one per component, and each
> exercises **save** - `ReviewForm`'s post, the email editor's save and the notice editor's save. Reset and restore
> were changed the same way (`email-editor.tsx`, `email-versions.tsx`, `wording-editor.tsx`) without a test of their
> own. The pull request's evidence records the revert check for the two editors' tests; for `ReviewForm`'s, only
> this plan and tasks.md say so.

## Research

- **D1 - compose in CI, not `dotnet run`.** The flows need the gateway, the storefront's nginx, Mailpit and SeaweedFS
  as well as the services. Compose is how a person runs them, and it is one command.
- **D2 - data through the API, pages through the browser.** Setting a seller up through five screens would test the
  shop-application screens four times over. The flows click what the issue names, and the rest is made the way
  Bruno makes it.

These two are D4 and D5 in [research.md](research.md), which records every decision with its alternatives - D1
Playwright, D2 Edge locally, D3 Chromium in CI, and the waits, paging, language, clean-up and the lost-toast fix.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

Stated plainly, so the next reader does not take four green flows for more than they are:

- **One browser engine.** Edge and Chromium are both Chromium; Firefox and WebKit are never run, and neither is a
  phone-sized viewport.
- **Local Edge and CI Chromium are different builds.** A flow could pass in one and fail in the other; none was seen
  at the merge.
- **Four flows, one branch each.** Only payment approval is exercised (the job sets `PAYMENT_OUTCOME=Approve`);
  cancellation, returns, vouchers, questions, saved products, the Vietnamese storefront and every other page are not
  driven in a browser. Their coverage is still the unit tests, Bruno and `verify-saga.sh`.
- **People and orders accumulate.** A run removes its products and category only; its three accounts, the shop, the
  address and the order stay (see [data-model.md](data-model.md)). `cleanUp` does not check its deletes' statuses, so
  a product that fails to delete is left without a word - `seed/clean-test-debris.py` is the backstop.
- **Sign-ins are not economised.** Each `admin()` call signs in again, and the gateway allows 30 sign-in requests a
  minute per client IP (specs/062). A run stays under that by a count of the code at the merge; it was not measured.
- **Against the containers only, as recorded.** `E2E_BASE_URL` could point at Vite's dev server, but a run against a
  `start-dev` stack is not recorded, and Mailpit is needed either way.
- **The lost-toast fix is tested on save only** (see the note under "Found on the way"). Nothing stops the same
  mistake - a success callback handed to `mutate()` in a component whose key the save changes - being written again
  elsewhere; CLAUDE.md now warns about it.
