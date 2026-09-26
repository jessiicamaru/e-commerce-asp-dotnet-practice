# Phase 0 Research: The storefront in a real browser

> Written on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Issue**: #117

The decisions below were settled while building the suite. Two of them (D4, D5) were written in
[plan.md](./plan.md) at the time as "D1" and "D2"; they are renumbered here and kept word for word there. Where the
record names no rejected alternative, this file says so rather than supplying one.

---

## D1 - Playwright as the browser driver

**Decision**: `@playwright/test` (`^1.63.0`, a dev dependency of `client/`), one config at
`client/playwright.config.ts`, the suite in `client/e2e/`, run with `npm run e2e` (`playwright test`).

**Rationale**: The issue names it ("Playwright in `client/`"). It also serves both halves of the suite from one
tool: `page` drives the browser, and `APIRequestContext` (`request.newContext({ baseURL })`) makes the setup data
through the same base URL, so setup requests pass through the storefront's nginx and the gateway exactly as the
pages' requests do. `expect.poll` and `toPass` give the bounded waits the flows need (D8), and its trace viewer is
what a person opens after a CI failure (D12).

**Alternatives considered**:

- **Cypress.** The issue's own evidence searched `client/package.json` for `playwright\|cypress` and found neither;
  the issue then asked for Playwright. Why Cypress was not chosen is not recorded.

---

## D2 - Locally, the Microsoft Edge that Windows already has

**Decision**: The Playwright project uses `devices['Desktop Chrome']` with `channel: 'msedge'` unless
`E2E_BROWSER_CHANNEL` says otherwise:

```ts
const channel = process.env.E2E_BROWSER_CHANNEL ?? 'msedge'
...
use: { ...devices['Desktop Chrome'], ...(channel ? { channel } : {}) },
```

So nothing is downloaded on a developer's machine, and `npx playwright install` is never run there.

**Rationale**: Decided by the user: Playwright's own browser is 150-250 MB on drive C:. Edge is Chromium, as
Playwright's own browser is, so a flow that passes in one is exercising the same engine as the other.

**Alternatives considered**:

- **Playwright's bundled Chromium locally** (`npx playwright install chromium`). Rejected by the user for its size
  on drive C:.

---

## D3 - In CI, Playwright's Chromium, in a `browser-e2e` job

**Decision**: CI sets `E2E_BROWSER_CHANNEL: ''` - an empty string, which the `??` above keeps, and which the
spread then treats as "no channel" - and installs the browser on the runner with
`npx playwright install --with-deps chromium`. The job is `browser-e2e` (*Browser end-to-end*) on `ubuntu-latest`,
`needs: [saga-e2e, client]`, `timeout-minutes: 40`; `publish` gained it in its `needs`.

**Rationale**: The runner is thrown away after the job, so a download costs nothing there, and the config comment
records the split ("a developer's machine uses the Edge it already has"). After the saga job because the issue asks
for it there and, as the workflow's comment says, "a checkout that cannot settle is reported there first, in
words" - `saga-e2e` explains a broken checkout better than a browser timing out on "Paid". `publish` waits for it
because "a storefront whose pages cannot reach the API is not a release".

**Alternatives considered**:

- **Edge in CI as well.** Not recorded.
- **Running it in the `client` job.** Not recorded; that job builds no services.

---

## D4 - Compose in CI, not `dotnet run` (plan.md's D1)

**Decision**: The job writes a throwaway `server/.env` and runs
`docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build --wait --wait-timeout 600`.

**Rationale**: The flows need the gateway, the storefront's nginx, Mailpit and SeaweedFS as well as the services.
Compose is how a person runs them, and it is one command. `--wait` returns only when every container with a health
check reports healthy - which since specs/071 includes the orchestrator.

**Alternatives considered**:

- **`dotnet run` per service, as `auth-smoke` and `saga-e2e` do.** Rejected: it would leave the storefront container
  (nginx on `:8088`, forwarding `/api`), Mailpit and SeaweedFS to be started by other means, and the nginx hop is
  part of what the flows exist to cover.

---

## D5 - Data through the API, pages through the browser (plan.md's D2)

**Decision**: `client/e2e/support/api.ts` (`Api`) makes everything a flow starts from through the gateway, with
real signed tokens: a category; a seller registered, confirmed through Mailpit and approved; the seller's two
products listed, one approved and stocked; a customer with an address; a moderator granted by the administrator.
The browser clicks only the four flows.

**Rationale**: Setting a seller up through five screens would test the shop-application screens four times over.
The flows click what the issue names, and the rest is made the way Bruno makes it. Going through the API rather
than SQL keeps the setup on the path a person uses, so a setup request exercises the gateway routes too - the same
reason `seed/seed-catalogue.py` posts through the gateway.

**Alternatives considered**:

- **Everything through the pages.** Rejected, above: repeated coverage of screens the flows are not about, and a
  slower run.
- **Writing rows with SQL.** Rejected: it would skip the outbox, the events and the gateway - Inventory would never
  hear of the product, and a missing route in the setup path would go unnoticed.

---

## D6 - One serial group, one worker

**Decision**: `test.describe.serial(...)` holding the four flows, with `fullyParallel: false`, `workers: 1`,
`timeout: 90_000` per test and `expect.timeout: 20_000`. The order id is read from the URL after checkout and
carried to the seller's flow and the review.

**Rationale**: "One stack, shared state: the flows run in order, and a real checkout is seconds, not milliseconds"
(the config's comment). The flows are one story - the order the customer places is the one the seller ships and
the customer reviews - so a failure early skips the rest instead of running them against a state they did not
expect.

**Alternatives considered**:

- **Independent tests, each making its own order.** Not recorded as considered. Each would repeat the setup and a
  full checkout through the saga.

---

## D7 - Remove the products and the category; leave the people

**Decision**: `Api` records the ids of the products and category it creates; `afterAll` calls `cleanUp()`, which
deletes each product (`DELETE /api/products/{id}`) and then the category (`DELETE /api/categories/{id}`) as the
administrator - "what Bruno's teardown folder does (specs/073)".

**Rationale**: Before specs/073 the test scripts had left 97 products against 14 cameras; a suite that lists two
products on every run would repeat that. Deleting a product announces `ProductDeletedEvent`, so Inventory drops the
stock rows too. `afterAll` runs whether the flows passed or not.

**Alternatives considered**:

- **Deleting the accounts, the address and the order as well.** Not recorded. The accounts are tagged by the run
  (`e2e-<name>-<run>@local.test`) and remain.

---

## D8 - Bounded polls for what arrives through the broker

**Decision**: Every asynchronous fact is waited for with a limit and a message:

| What | How | Limit |
| :-- | :-- | :-- |
| The confirmation email in Mailpit | 40 attempts, a second apart | ~40 s |
| The stock row (a 404 until Catalog's `ProductCreated` reaches Inventory) | `expect.poll` on `PUT /api/stock/{variantId}` | 30 s |
| Catalog saying `InStock` | `expect.poll` on `GET /api/products/{id}` | 30 s |
| The order settling to *Paid* | `toBeVisible` on the page's words | 60 s |
| The right to review (`ParcelDeliveredEvent`) | `toPass`, reloading the product page | 45 s |
| The product `Approved` | `expect.poll` on `reviewStatus` | no limit named: Playwright's default for `expect.poll` |

**Rationale**: The stack is eventually consistent by design, and each of these is seconds behind the request that
caused it. A fixed sleep either waits too long on a fast machine or too little on a slow runner; a poll with a
message says, when it fails, which fact never arrived.

**Alternatives considered**: not recorded.

---

## D9 - Paging through a shared queue (`findOnPages`)

**Decision**: `findOnPages(page, item)` looks for the item on the current page for 5 seconds, then presses *Next*,
up to 50 pages, and throws with the "Showing ..." counter of every page it looked at.

**Rationale**: "A queue a machine shares with older runs, or with a person, is not guaranteed to hold this run's
item on its first page" (`PAGE_SIZE` is 12). Each page is given a few seconds rather than checked at once, because
the counter changes at the click while the list keeps the previous page's rows until the new page arrives - an
immediate check would read the old rows under the new counter.

**Alternatives considered**:

- **Wait for the counter to change after *Next*, then count the matching rows at once.** This was the first version
  (commit `8f56709` in the pull request) and was replaced in the next commit (`3633942`): the counter is not a
  signal that the rows have arrived, so the check could look at the previous page's rows and turn past the item.

The same commit tightened the last assertion of the review flow: the review's words are looked for in a `listitem`
of the list of reviews, "not only in the form it was typed into". What prompted it is not recorded.

---

## D10 - English, whatever the machine speaks

**Decision**: `signIn` adds an init script setting `localStorage.language = 'en'` before opening `/sign-in`, and
the browser context's `locale` is `en-US`.

**Rationale**: The flows find buttons and messages by their English words. The storefront otherwise takes the
language from the browser (specs/021), so the same suite would look for English words on one machine and meet
Vietnamese ones on another - the same reason the storefront's unit tests pin their language.

**Alternatives considered**:

- **Selecting by test ids instead of words.** Not recorded. Finding by role and label is also what checks that a
  control is named for a person.

---

## D11 - The lost toast: `mutateAsync().then(...)`

**Decision**: Where a save's success changes a component's `key`, the success toast is chained on the promise
from `mutateAsync` rather than handed to `mutate(vars, { onSuccess })`:

```tsx
write.mutateAsync({ rating, body: body.trim() }).then(
  () => toast.success(t('reviews.saved')),
  () => {},
)
```

Applied to `ReviewForm` (keyed by the review's id, so the first review remounts it), the email editor (save, reset)
and its version list (restore), and the notice-wording editor (save, reset, restore), each keyed by a version. The
rejection handler is empty because a refusal is already shown from the mutation's `error`.

**Rationale**: Found by the first run. The component is replaced before a callback handed to `mutate()` runs, and
TanStack Query does not run those callbacks for a component that is gone. The change was saved, but nobody was told.
A promise chained on `mutateAsync` belongs to the call, not to the component, so it runs regardless.

**Alternatives considered**: not recorded.

---

## D12 - Traces on failure only, uploaded with the services' logs

**Decision**: `trace: 'retain-on-failure'` and `screenshot: 'only-on-failure'`; in CI the reporter is `list` plus
an HTML report (`open: 'never'`), `forbidOnly` is on and `retries` is 0. On failure the job writes
`docker compose ... logs` to `client/test-results/compose.log` and uploads `client/test-results/` and
`client/playwright-report/` as `browser-e2e-traces`, kept 7 days. The three output directories are git-ignored.

**Rationale**: A browser failure is read in the trace viewer (`npx playwright show-trace <trace.zip>`), next to what
the services logged at the same moment; nothing is kept for a pass. No retries, because a flow that passes on its
second try is a flaky flow reported as a green one.

**Alternatives considered**: not recorded.

---

## D13 - The suite is its own TypeScript project

**Decision**: `client/tsconfig.e2e.json` (includes `e2e` and `playwright.config.ts`, `types: ["node"]`, strict,
`noEmit`) is referenced from `client/tsconfig.json`, so `tsc -b` - part of `npm run build` and the `client` CI job -
type-checks the suite. Vitest still only reads `src/`.

**Rationale**: A suite that only runs against a started stack would otherwise go unchecked on every change that
does not run it; a renamed export in the helpers is caught by the build instead.

**Alternatives considered**: not recorded.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| The flows run in Edge locally and Chromium in CI | A difference between the two versions could pass in one and fail in the other | Both are Chromium; none observed at the merge |
| `cleanUp` does not check the status of its deletes | A product that fails to delete is left silently | `seed/clean-test-debris.py` removes any product `cameras.json` does not name |
| Each `admin()` call signs in again, and the gateway limits sign-in to 30 a minute per client IP (specs/062) | A slower or longer suite could meet the limit | Not measured; a count of the code at the merge stays under it |
| Accounts, addresses and orders from every run remain | The admin user list grows by three accounts a run | Not addressed (see [plan.md](./plan.md), "What this feature does not finish") |
