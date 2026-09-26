# Feature Specification: The storefront in a real browser

> Completed on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Feature Branch**: `080-e2e-browser` | **Created**: 2026-09-26 | **Issue**: #117 (closes it)

**Status**: Merged (PR #164, 2026-09-26)

**Input**: Issue #117, "test: no test drives the storefront in a browser" - "Playwright in `client/`, against the
running compose stack, covering the flows the seed data sets up: buy and pay; a seller ships; the customer confirms
and reviews; a moderator approves a product. A CI job after the saga job, with the traces uploaded on failure.
Acceptance: the four flows pass in CI; a deliberately broken gateway route makes one fail."

## Why

The storefront has unit tests (Vitest), and the API has Bruno and `verify-saga.sh`, but nothing clicks through the
real pages against the real stack. A page and the API it calls drifting apart, such as a missing gateway route or a
renamed field, is found by a person.

Each existing layer is blind to it in its own way. A Vitest test replaces the API with a spy, so it proves a page
sends what the test author believes the server wants. Bruno and `verify-saga.sh` call the API directly, so they
prove the API answers, not that any page calls it the way it answers. The one place the two meet - the storefront
container's nginx forwarding `/api` to the gateway, the gateway's routes, the words a page shows after a real
response - had no check at all.

## User Scenarios & Testing *(mandatory)*

The people in these stories are the ones who maintain the shop - a developer changing a page or a route, and the
release pipeline deciding whether to publish. The flows themselves are four things a shopper, a seller and a
moderator do, and they appear as the acceptance scenarios of User Story 1.

### User Scenarios

Four flows, in order, each through the real pages:

1. **A moderator approves a product** waiting for review, from `/admin/products`.
2. **A customer buys**: product page, add to cart, cart, checkout, place order. The order settles to *Paid* through
   the saga.
3. **The seller ships** their parcel from `/shop/sales/:id`: start preparing, then mark as shipped with a tracking
   reference.
4. **The customer confirms it arrived and reviews** the product once the right to review arrives.

---

### User Story 1 - A page that cannot reach its API fails a check before a person finds it (Priority: P1)

A developer renames a gateway route, a field or a page's words. Before this feature, the change passed every check
and the first sign of it was a person clicking a button that did nothing. Now four flows a person really takes are
clicked through the real pages against the real stack, and one of them fails.

**Why this priority**: This is the whole of issue #117. Every other story is how it runs (locally, in CI) or how it
stays trustworthy (fresh data, clean-up).

**Independent Test**: Start the compose stack, run `npm run e2e` in `client/` and see four flows pass; rename the
gateway's cart route, rebuild the gateway, run again and see the buying flow fail.

**Acceptance Scenarios**:

1. **Given** a seller's product waiting for review, **When** a moderator signs in, opens `/admin/products`, finds it
   (turning pages if needed) and presses *Approve*, **Then** the page says "“<name>” is on sale." and Catalog reports
   the product `Approved`.
2. **Given** an approved product with 5 units in stock and a customer with a saved address, **When** the customer
   opens the product page, adds it to the cart, opens the cart, checks out and places the order, **Then** the page
   moves to `/orders/<id>` and, within 60 seconds, says "Paid. We will start preparing it soon." - the saga reserved
   the stock and the stub gateway approved.
3. **Given** that paid order, **When** its seller opens `/shop/sales`, opens the sale, presses *Start preparing*,
   then *Mark as shipped* with the tracking reference `VNPOST-E2E-1`, **Then** the page says "Marked as shipped." and
   shows "Tracking reference: VNPOST-E2E-1".
4. **Given** that shipped order, **When** the customer opens it, presses *I've received it* and confirms, **Then** the
   page says "Thanks for confirming."; and once the right to review has arrived from Order's `ParcelDeliveredEvent`,
   a 5-star review posted from the product page says "Thank you - your review is up." and appears in the list of
   reviews, not only in the form.
5. **Given** the gateway's cart route renamed, **When** the flows run, **Then** the buying flow fails.

---

### User Story 2 - The release waits for the browser (Priority: P2)

A pull request or a merge runs the same four flows in CI, against the stack built from that commit, and `publish`
does not run unless they passed. When one fails, a person can see why without reproducing it.

**Why this priority**: A check that runs only on one developer's machine protects only that developer's changes.
It is second because it adds nothing until User Story 1 exists.

**Independent Test**: Open the CI run of a pull request and see a `Browser end-to-end` job after `Saga
end-to-end`; read `.github/workflows/ci.yml` and see `browser-e2e` in `publish`'s `needs`.

**Acceptance Scenarios**:

1. **Given** a push or a pull request to `main`, **When** CI runs, **Then** the `browser-e2e` job starts after
   `saga-e2e` and `client`, builds and starts the whole stack with compose, and runs the flows in Playwright's
   Chromium.
2. **Given** a flow fails in CI, **When** the job ends, **Then** the traces, the HTML report and every service's
   compose log are uploaded as the `browser-e2e-traces` artifact, kept 7 days.
3. **Given** the `browser-e2e` job failed, **When** the run reaches `publish`, **Then** nothing is published.

---

### User Story 3 - A developer runs the flows without downloading a browser (Priority: P3)

On the developer's Windows machine the flows run in the Microsoft Edge that Windows already has. Nothing is
installed to drive C:.

**Why this priority**: Decided by the user: Playwright's own browser is 150-250 MB on drive C:. It shapes how the
suite is configured but not what it checks, so it follows the two stories that define the check.

**Independent Test**: On a machine where `npx playwright install` has never been run, `npm run e2e` starts Edge and
the flows run.

**Acceptance Scenarios**:

1. **Given** `E2E_BROWSER_CHANNEL` is not set, **When** `npm run e2e` runs, **Then** Playwright drives Edge
   (`channel: 'msedge'`).
2. **Given** `E2E_BROWSER_CHANNEL` is set to an empty string, **When** the flows run, **Then** Playwright uses its
   own Chromium - which is what CI does.

---

### User Story 4 - Every run starts from its own data and removes what it listed (Priority: P4)

A run makes its own people and products through the API, so it does not depend on what somebody seeded, and removes
its products and category at the end, so it does not fill the catalogue with debris.

**Why this priority**: Without it the flows would pass or fail depending on the machine. It is last because it is
how the other stories stay honest rather than something a person sees.

**Independent Test**: Run the flows twice in a row on the same stack; both pass, and after each, no product named
`E2e camera <run>` or `E2e waiting <run>` is listed.

**Acceptance Scenarios**:

1. **Given** a stack with products and orders from earlier runs or from a person, **When** the flows run, **Then**
   they find this run's product in a shared queue by turning pages, and this run's sale by being the only sale of a
   seller made for this run.
2. **Given** the flows have finished, whether they passed or not, **When** `afterAll` runs, **Then** each product
   this run listed and then its category are deleted through the API as the administrator.

---

### Edge Cases

- **This run's product is not on the first page of the moderation queue.** The queue is shared with older runs and
  with people. The flow turns pages (up to 50), giving each page 5 seconds to show the product, because after
  *Next* the counter ("Showing 13-14") changes at the click while the rows arrive with the next response.
- **The stock row has not arrived yet.** Inventory learns of a product from Catalog's event; until then
  `PUT /api/stock/{variantId}` is the "not arrived yet" 404 (specs/031). The setup retries it for up to 30 seconds.
- **Catalog's availability is behind Inventory.** Catalog hears of stock from Inventory's event, seconds later. The
  setup does not hand the product to the flows until Catalog's own read model says `InStock`, waiting up to 30
  seconds. (The product page's *Add to cart* itself reads Inventory's live count, `GET /api/stock/{id}`.)
- **The confirmation email is not in Mailpit yet.** Identity sends it through its outbox and a dispatcher; the setup
  asks Mailpit up to 40 times, a second apart.
- **The Seller role is not in the token.** A role reaches a session at its next sign-in (specs/044), so the seller
  and the moderator sign in again after being granted it.
- **The right to review arrives through the broker.** The product page is reloaded until "Review this product"
  shows, for up to 45 seconds.
- **Settling to Paid takes the saga seconds.** The flow waits up to 60 seconds for the *Paid* words.
- **The machine speaks Vietnamese.** Every sign-in sets the storefront's language to English first and the browser
  locale is `en-US`, so the words the flows look for are the same on every machine and in CI.
- **A flow fails.** The flows are one serial group, so the ones after it are skipped rather than run against a
  state they did not expect; the clean-up still runs.
- **`ADMIN_EMAIL` or `ADMIN_PASSWORD` is not set.** The setup throws with a sentence saying the flows sign in as the
  seeded administrator.
- **A saved change remounts its form.** Found by the first run: saving the first review, or saving, resetting or
  restoring an email or a notice, gives the component a new `key`; a callback handed to `mutate()` does not run for
  a component that is gone, so the change was saved and nobody was told.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** Playwright in `client/` (`e2e/`, `npm run e2e`) against the compose stack, through the storefront
  container on `:8088`.
- **FR-002** Each run makes its own people and products through the API (sellers are confirmed through Mailpit and
  approved) and removes its products and category.
- **FR-003** The browser:
  - locally, the **Microsoft Edge Windows already has** (`channel: 'msedge'`), so nothing is downloaded;
  - in CI, Playwright's Chromium (`E2E_BROWSER_CHANNEL` empty).
  - The user decided this: Playwright's own browser is 150-250 MB on drive C:.
- **FR-004** A CI job after the saga job:
  - compose builds and starts the whole stack with throwaway values;
  - the traces, the report and the services' logs are uploaded on failure;
  - `publish` waits for it.
- **FR-005** Playwright's output (`test-results/`, `playwright-report/`) is git-ignored.
- **FR-006** Nothing in the path under test is mocked: the browser talks to the storefront's nginx, which forwards
  `/api` to the gateway, which routes to the services.
- **FR-007** The flows MUST run in order in one worker, carrying state from one to the next (the order the customer
  placed is the one the seller ships and the customer reviews).
- **FR-008** Setup data MUST be made through the gateway with real signed tokens, the way a person's clicks reach
  it - never by writing SQL.
- **FR-009** Every wait for something that happens asynchronously (a message through the broker, a read model, an
  email) MUST be a bounded poll with a message saying what it waited for, never an unbounded wait.
- **FR-010** A flow MUST sign in through the sign-in page, in English, whatever the machine's language.
- **FR-011** A failure MUST keep a trace and a screenshot; a pass keeps neither.
- **FR-012** The e2e sources MUST be type-checked by the storefront's `tsc -b`, and MUST NOT be read by Vitest.
- **FR-013** In CI, a `.only` left in a flow MUST fail the run, and a failing flow MUST NOT be retried into a pass.
- **FR-014** A save whose success changes a component's `key` MUST still tell the person it was saved.

### Key Entities

- **Flow**: one of the four things a person does, as a Playwright test in `e2e/flows.spec.ts`. The four share one
  serial `describe`.
- **Run**: one execution of the suite, tagged by the time it started (`Date.now()`), which names every account,
  shop, category and product it makes so two runs never collide.
- **Setup helper** (`Api`): makes the run's people and products through the gateway and records the products and
  category it made, for the clean-up.
- **Trace**: Playwright's recording of a failed flow - DOM snapshots, network, console - opened with
  `npx playwright show-trace`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The four flows pass - locally in Edge, three runs in a row; in CI in Chromium (the pull request's
  `Browser end-to-end` job passed, in 5m14s).
- **SC-002**: A deliberately broken gateway route makes one flow fail. Verified: the cart route renamed made the
  buying flow fail.
- **SC-003**: Running the flows locally downloads no browser.
- **SC-004**: `publish` cannot run unless `browser-e2e` passed.
- **SC-005**: After a run, none of the products or the category it listed remain.
- **SC-006**: The defect the first run found is fixed in every place it occurred, with a unit test that unmounts
  before the save answers; the storefront's unit tests pass (453 of 453, in 81 files, at the merge).

## Acceptance

- The four flows pass. Locally they passed three runs in a row in Edge, and in CI in Chromium.
- A deliberately broken gateway route makes one fail. Verified: the cart route renamed made the buying flow fail.

## Assumptions

- The stack is the compose stack with the app overlay (`docker-compose.yml` + `docker-compose.app.yml`): the
  services, the gateway, the storefront container, Mailpit and SeaweedFS. A stack started with `start-dev` has no
  storefront container on `:8088`; `E2E_BASE_URL` can point elsewhere, but running the flows against anything else is
  not recorded.
- The seeded administrator's credentials are in `ADMIN_EMAIL` / `ADMIN_PASSWORD`.
- Payment approves (`PAYMENT_OUTCOME=Approve`, which the CI job writes into its `.env`): the buying flow expects
  *Paid*, and against a Payment configured to reject it fails.
- The shop's default currency prices the product (`price: 1_250_000` is the default currency's amount, specs/028)
  and the address is in Vietnam, so a delivery option is offered.
- Windows has Microsoft Edge installed, which it does by default.

## Out of Scope

- Browsers other than Chromium-based ones (Firefox, WebKit), and phone-sized viewports.
- The Vietnamese storefront, and prices in any currency but the default.
- The payment-rejection branch, cancellation, returns, vouchers, questions and every other flow - the four are the
  ones the issue names.
- Removing the accounts, the address and the order a run makes (see [data-model.md](./data-model.md)).
- Visual regressions: a layout fault is found in a screenshot by a person, as the client README says.
