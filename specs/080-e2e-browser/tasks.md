---
description: "Task list for the storefront in a real browser"
---

# Tasks: The storefront in a real browser

> Completed on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Input**: Design documents from `/specs/080-e2e-browser/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: The feature is a test suite. Beyond the flows themselves, the defect they found carries unit tests
(Vitest) that unmount before the save answers, and the suite was shown able to fail by breaking a gateway route -
constitution Principle V.

**Organization**: The six tasks as merged come first, unchanged. Below them the same work is broken down task by task
(T007 onward), grouped by user story, in the order it has to happen.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US4)

---

## Tasks as merged

- [X] T001 Playwright in `client/`: the config (Edge locally, Chromium in CI), the tsconfig, `npm run e2e`, and
  Playwright's output ignored.
- [X] T002 API setup and clean-up (`e2e/support/api.ts`), sign-in and paging helpers (`e2e/support/ui.ts`).
- [X] T003 The four flows (`e2e/flows.spec.ts`), passing three runs in a row against the local stack.
- [X] T004 The lost-toast defect they found: `ReviewForm` and the email and notice editors, each with a unit test
  that fails when reverted.
- [X] T005 The broken-route check (the cart route renamed makes the buying flow fail), the CI job with traces, and
  `publish` gated on it.
- [X] T006 The docs: testing strategy, the client README, CLAUDE.md, counts, timeline and backlog.

> Note on T004, 2026-09-27: the code at the merge has one such test per component, each on **save**; reset and
> restore were fixed the same way without their own test. The pull request records the revert check for the two
> editors' tests. See plan.md, "Found on the way".

---

## Phase 1: Setup (T001)

**Purpose**: Playwright in the storefront, without a browser download.

- [X] T007 Add `@playwright/test` (`^1.63.0`) to `devDependencies` and the script `"e2e": "playwright test"` in `client/package.json` (lock file updated in `client/package-lock.json`); do **not** run `npx playwright install` locally
- [X] T008 Create `client/playwright.config.ts`: `testDir: './e2e'`, `fullyParallel: false`, `workers: 1`, `timeout: 90_000`, `expect.timeout: 20_000`, `forbidOnly` and an HTML report in CI, `retries: 0`, `baseURL` from `E2E_BASE_URL` (default `http://localhost:8088`), `trace: 'retain-on-failure'`, `screenshot: 'only-on-failure'`, `locale: 'en-US'`, one project `storefront` on `devices['Desktop Chrome']` with `channel` from `E2E_BROWSER_CHANNEL` (default `msedge`, empty for Playwright's Chromium) - research D2, D3, D6, D12
- [X] T009 [P] Create `client/tsconfig.e2e.json` (includes `e2e` and `playwright.config.ts`, `types: ["node"]`, strict, `noEmit`) and reference it from `client/tsconfig.json`, so `tsc -b` checks the suite and Vitest still reads only `src/` - research D13
- [X] T010 [P] Ignore `test-results/`, `playwright-report/` and `blob-report/` in `client/.gitignore` (FR-005)

**Checkpoint**: `npx tsc -b` passes with the new project; `npm run e2e` starts Edge and finds no tests yet.

---

## Phase 2: Foundational (T002)

**Purpose**: The data every flow starts from, and the two helpers every flow uses. No flow can run before this.

- [X] T011 Create `Api` in `client/e2e/support/api.ts` with `login`, `admin` (from `ADMIN_EMAIL` / `ADMIN_PASSWORD`, throwing a sentence when either is unset), `customer` (`POST /api/auth/register`) and a run tag `Date.now()` naming every account `e2e-<name>-<run>@local.test` - research D5
- [X] T012 Add `confirmEmail` to `client/e2e/support/api.ts`: search Mailpit (`E2E_MAILPIT_URL`, default `:8025`) for the address up to 40 times a second apart, read `confirm-email?token=...` from the text, `POST /api/auth/confirm-email` and expect 204 - research D8
- [X] T013 Add `seller` to `client/e2e/support/api.ts`: `register-seller`, `confirmEmail`, `GET /api/shop-applications/mine`, approve as the administrator, then sign in again because the Seller role reaches a session at its next sign-in (specs/044); and `moderator`: register, `PUT /api/users/{id}/roles/Moderator` as the administrator, sign in again
- [X] T014 Add `category`, `list` (a seller's product at `1,250,000` in the default currency, its first variant reusing its id - specs/020), `approve`, `address` and `productReviewStatus` to `client/e2e/support/api.ts`, recording each product and category it makes
- [X] T015 Add `stock` to `client/e2e/support/api.ts`: poll `PUT /api/stock/{variantId}` for up to 30 s until 200 (the "not arrived yet" 404, specs/031), then poll `GET /api/products/{id}` for up to 30 s until Catalog says `InStock` - research D8
- [X] T016 Add `cleanUp` to `client/e2e/support/api.ts`: delete each product the run listed, then its category, as the administrator - as Bruno's teardown does (specs/073), research D7
- [X] T017 [P] Create `signIn` in `client/e2e/support/ui.ts`: set `localStorage.language = 'en'` in an init script, fill *Email* and *Password* on `/sign-in`, press *Sign in*, expect to leave the page - research D10
- [X] T018 [P] Create `findOnPages` in `client/e2e/support/ui.ts`: look for the item for 5 s per page, press *Next* up to 50 times, throw with every "Showing ..." counter seen - research D9

**Checkpoint**: a `beforeAll` using `Api` leaves a stocked, approved camera, a waiting product, a customer with an
address, a seller and a moderator on the local stack.

---

## Phase 3: User Story 1 - A page that cannot reach its API fails a check (P1) 🎯 MVP (T003, T005)

**Goal**: The four flows through the real pages, and proof that they can fail.

**Independent test**: quickstart scenarios 1 and 2.

- [X] T019 [US1] Create `client/e2e/flows.spec.ts` with one `test.describe.serial`, a `beforeAll` making the category, the seller, the approved camera stocked with 5, the waiting product, the customer and address, and the moderator, and an `afterAll` calling `cleanUp` and disposing the request context
- [X] T020 [US1] Flow 1 in `client/e2e/flows.spec.ts`: the moderator signs in, opens `/admin/products`, finds the waiting product with `findOnPages`, presses *Approve*, sees "“<name>” is on sale." and the product reads `Approved`
- [X] T021 [US1] Flow 2 in `client/e2e/flows.spec.ts`: the customer opens the camera, *Add to cart* ("Added 1 to your cart."), `/cart`, *Check out*, finds the saved address checked, *Place order*, lands on `/orders/<id>` (the id kept for the next flows) and sees "Paid. We will start preparing it soon." within 60 s
- [X] T022 [US1] Flow 3 in `client/e2e/flows.spec.ts`: the seller opens `/shop/sales`, the sale, *Start preparing* ("Marked as being prepared."), *Mark as shipped* with `VNPOST-E2E-1` in the dialog, and sees "Marked as shipped." and the tracking reference
- [X] T023 [US1] Flow 4 in `client/e2e/flows.spec.ts`: the customer opens the order ("On its way."), *I've received it*, *Yes, it arrived* ("Thanks for confirming."), reloads the product until "Review this product" shows (up to 45 s, `ParcelDeliveredEvent` through the broker - specs/046), posts a 5-star review and finds it in the list of reviews, not only in the form
- [X] T024 [US1] Run the flows against the local compose stack in Edge until they pass three runs in a row
- [X] T025 [US1] Negative control: rename the cart route in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, rebuild the gateway, see the buying flow fail, restore the route (not committed) - SC-002

**Checkpoint**: quickstart scenarios 1 and 2 pass. This is the MVP - the issue's acceptance, locally.

---

## Phase 4: The defect the first run found (T004)

**Goal**: A save whose success changes a component's `key` still tells the person (FR-014).

- [X] T026 [P] [US1] In `client/src/components/product/product-reviews/index.tsx`, post the review with `write.mutateAsync(...).then(toast, () => {})` instead of `mutate(..., { onSuccess })` - `ReviewForm` is keyed by the review's id, so the first review remounts it
- [X] T027 [P] [US1] Add to `client/src/components/product/product-reviews/index.test.tsx` a test that posts, unmounts before `Reviews.write` resolves, then resolves it and expects the "Thank you - your review is up." toast
- [X] T028 [P] [US1] In `client/src/pages/admin-emails/email-editor.tsx` (save, reset) and `client/src/pages/admin-emails/email-versions.tsx` (restore), chain the toast on `mutateAsync` - the editor is keyed by template, language and version
- [X] T029 [P] [US1] Add to `client/src/pages/admin-emails/index.test.tsx` a test that saves, unmounts before `EmailTemplates.save` resolves, and expects "Saved. The next email says this."; revert the save's fix and see it fail
- [X] T030 [P] [US1] In `client/src/pages/admin-wording/wording-editor.tsx` (save, reset, restore), chain the toast on `mutateAsync` - the editor is keyed by the line and its version
- [X] T031 [P] [US1] Add to `client/src/pages/admin-wording/index.test.tsx` a test that saves, unmounts before `NotificationWording.save` resolves, and expects "Saved. Readers see it on their next load."; revert the save's fix and see it fail
- [X] T032 [US1] Run the storefront's unit tests, lint and `tsc -b`: 453 of 453 in 81 files, clean

**Checkpoint**: quickstart scenario 7 passes.

---

## Phase 5: User Story 2 - The release waits for the browser (P2) (T005)

**Goal**: The same flows in CI, gating `publish`.

**Independent test**: quickstart scenario 4.

- [X] T033 [US2] Add the job `browser-e2e` (*Browser end-to-end*) to `.github/workflows/ci.yml`: `needs: [saga-e2e, client]`, `timeout-minutes: 40`, a throwaway `server/.env` written inline (`PAYMENT_OUTCOME=Approve`, `STOREFRONT_URL=http://localhost:8088`, CI-only credentials), `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build --wait --wait-timeout 600` - research D4
- [X] T034 [US2] In the same job: Node 22 with the npm cache on `client/package-lock.json`, `npm ci`, `npx playwright install --with-deps chromium`, and `npx playwright test` with `E2E_BROWSER_CHANNEL: ''`, `E2E_BASE_URL`, `E2E_MAILPIT_URL`, `ADMIN_EMAIL`, `ADMIN_PASSWORD` - research D3
- [X] T035 [US2] On failure, write `docker compose ... logs` to `client/test-results/compose.log` and upload `client/test-results/` and `client/playwright-report/` as `browser-e2e-traces`, kept 7 days - research D12
- [X] T036 [US2] Add `browser-e2e` to `publish`'s `needs` in `.github/workflows/ci.yml`, with the comment "a storefront whose pages cannot reach the API is not a release"

**Checkpoint**: the pull request's run passed *Browser end-to-end* (5m14s).

---

## Phase 6: User Stories 3 and 4 - Edge locally, fresh data and clean-up

Delivered by tasks above rather than by tasks of their own:

- US3 (Edge locally, nothing downloaded) is T007 and T008's `channel` default; quickstart scenario 5.
- US4 (every run its own data, its products removed) is T011-T016 and T018; quickstart scenario 6.

---

## Phase 7: Polish and documentation (T006)

- [X] T037 [P] Add the *Browser end to end* row to the layer table and a *Browser end to end* section to `docs/testing/testing-strategy.md` (how to run, the browser, the flows, the data, what was verified, reading a trace, the lost-toast lesson); update the storefront test count to 453
- [X] T038 [P] Add "In a real browser too" to `client/README.md`: `npm run e2e`, Edge, `e2e/` its own TypeScript project that Vitest never reads
- [X] T039 [P] Update `CLAUDE.md`: Playwright since specs/080, Edge locally, Chromium in `browser-e2e`, `publish` waits for it, and the ⚠️ on `mutate()` callbacks for a component whose key the save changes
- [X] T040 [P] Update the counts in `docs/overview/project-overview.md` (453 storefront tests, 68 design records, 85 merged pull requests), add the specs/080 row to `docs/project/timeline.md`, and move #117 to Fixed in `docs/project/backlog.md` - the backlog is now empty
- [X] T041 Write `specs/080-e2e-browser/spec.md`, `plan.md` and `tasks.md`
- [X] T042 Open and merge the pull request: **PR #164**, "test(client): Playwright drives the storefront against the real stack - four flows, Edge locally, Chromium in CI", closing #117, merged 2026-09-26

---

## Dependencies & Execution Order

- **Setup (Phase 1)** blocks everything.
- **Foundational (Phase 2)** blocks every flow: a flow cannot start without its people and products.
- **US1 flows (Phase 3)**: T019 before T020-T023; the four flows are serial by design (the order id is carried), so
  T021 before T022 before T023. T024 and T025 after all four.
- **The defect (Phase 4)** was found by T024's first run; its six changes are independent of each other ([P]), and
  T032 after them.
- **CI (Phase 5)** after Phase 3 and 4 pass locally; T036 last.
- **Docs (Phase 7)** at the end; T042 last of all.

### Parallel Opportunities

- T009 and T010 - separate files.
- T017 and T018 - two functions in one new file, written independently of `api.ts`.
- T026-T031 - three components and their three test files.
- T037-T040 - separate documents.

---

## Implementation Strategy

### MVP scope

Phases 1-3 (T007-T025): the four flows passing locally, and one of them failing when a route breaks. That is the
issue's acceptance on one machine.

### Incremental delivery

1. Setup + Foundational → a run's data can be made and removed
2. **+ US1 → MVP.** Four flows pass, and can fail
3. + the defect → the first thing the suite found is fixed and pinned by unit tests
4. + US2 → CI runs it and `publish` waits
5. + docs

### What this does not finish

One browser engine, four flows on the approval branch, and accounts and orders left behind on every run - see
[plan.md](./plan.md), "What this feature does not finish".

## Notes

- 42 tasks: 6 as merged, then 4 setup, 8 foundational, 7 for the flows, 7 for the defect, 4 for CI, 6 polish and
  delivery.
- Three commits in the pull request: `8f56709` the suite and the lost-toast fix; `3633942` the CI job, with two
  corrections to the flows (`findOnPages` giving each page 5 s instead of trusting the counter, and the review
  looked for in the list rather than anywhere on the page - research D9); `1f5c214` the docs and this record.
