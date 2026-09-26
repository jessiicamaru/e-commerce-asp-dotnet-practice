# Quickstart: Validating the storefront in a real browser

> Written on 2026-09-27, after the feature merged (#164), from the code at that merge, the pull request and docs/testing/testing-strategy.md (with client/README.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [contracts/README.md](./contracts/README.md)

How to prove the suite does what it claims. Scenarios 1 and 2 are the issue's acceptance; the rest check the
pieces around it. What was actually run at the merge is recorded under each scenario.

---

## Prerequisites

The whole stack in containers - the services, the gateway, the storefront on `:8088`, Mailpit and SeaweedFS:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build --wait
```

`server/.env` needs the usual values (`DB_*`, `RABBITMQ_*`, `JWT_SECRET`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`,
`SEQ_ADMIN_PASSWORD`, `SEAWEEDFS_*`) and `PAYMENT_OUTCOME` must not be `Reject`, or the buying flow waits for *Paid*
and fails. Before trusting a result, no stray `dotnet run` may be shadowing a container's port
(`Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }` must be empty).

Then, once, in the storefront:

```bash
cd client
npm ci
```

**Do not run `npx playwright install` on a developer's machine** - locally the flows use the Edge Windows already
has (research D2).

---

## Scenario 1 - The four flows pass (US1, SC-001)

```bash
cd client
export ADMIN_EMAIL=... ADMIN_PASSWORD=...     # the seeded administrator, as in server/.env
npm run e2e
```

A prefix assignment works here too (`ADMIN_EMAIL=... ADMIN_PASSWORD=... npm run e2e`): Playwright reads the
variables from its own environment. The Bruno trap in CLAUDE.md is about `"$ADMIN_EMAIL"` being expanded on the
same command line, which this command does not do.

**Expected**: the `list` reporter prints four passing tests under *the storefront, end to end*:

1. a moderator approves a product waiting for review;
2. a customer puts a camera in the cart, checks out and it is paid;
3. the seller prepares the parcel and ships it;
4. the customer says it arrived and reviews the camera.

**Recorded at the merge**: 4 of 4, three runs in a row, in Edge.

---

## Scenario 2 - A broken gateway route turns a flow red (US1 scenario 5, SC-002)

The negative control: a check that cannot fail proves nothing.

1. In `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, rename the cart route's path (for example
   `/api/cart/{**catch-all}` to `/api/carts/{**catch-all}`).
2. Rebuild and restart the gateway:
   `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build gateway`.
3. `npm run e2e`.
4. Restore the route and rebuild the gateway again.

**Expected**: the buying flow fails and the two after it are skipped (the group is serial).

**Recorded at the merge**: renaming the cart route made the buying flow fail; it was restored afterwards. Exactly
which path was used and at which assertion the flow stopped are not recorded.

---

## Scenario 3 - Read a failure (US2, research D12)

After a failed run:

```bash
cd client
npx playwright show-trace test-results/<test>/trace.zip
```

**Expected**: the trace viewer opens on the failing flow with each action, the page as it was, the network requests
and the console. A passing run leaves no trace. `test-results/`, `playwright-report/` and `blob-report/` do not
appear in `git status`.

---

## Scenario 4 - CI runs it and `publish` waits (US2, SC-004)

Open the run of any pull request to `main`: the *Browser end-to-end* job starts after *Saga end-to-end* and
*Storefront build*. In `.github/workflows/ci.yml`:

```bash
grep -n "needs: \[build, auth-smoke, saga-e2e, client, browser-e2e\]" .github/workflows/ci.yml
grep -n "E2E_BROWSER_CHANNEL: ''" .github/workflows/ci.yml
```

**Expected**: both lines found. On a failure the run carries a `browser-e2e-traces` artifact with the traces, the
HTML report and `compose.log`.

**Recorded at the merge**: the pull request's run passed *Browser end-to-end* in 5m14s; *Publish images* was skipped,
as it is for every pull request.

---

## Scenario 5 - Edge locally, Chromium when asked (US3, SC-003)

```bash
cd client
npm run e2e                               # Edge: a Microsoft Edge window runs the flows
E2E_BROWSER_CHANNEL= npm run e2e          # Playwright's Chromium - fails locally unless it was installed
```

**Expected**: the first runs in Edge with nothing downloaded. The second is what CI does after
`npx playwright install --with-deps chromium`; on a machine that followed the rule above it fails for want of the
browser, which is the rule working. Not recorded as run locally.

---

## Scenario 6 - A run removes what it listed (US4, SC-005)

After scenario 1, look for the run's products through the gateway:

```bash
curl -s "http://localhost:5000/api/products?searchTerm=E2e&pageSize=50" | jq '.items[].name'
```

**Expected**: no `E2e camera <run>` or `E2e waiting <run>` from the run just finished. (Products from runs that
stopped before `afterAll`, or from before this suite, are what `seed/clean-test-debris.py` is for.) Not recorded as
checked at the merge beyond the suite's own `cleanUp`.

---

## Scenario 7 - The lost toast stays found (SC-006, research D11)

```bash
cd client
npx vitest run src/components/product/product-reviews src/pages/admin-emails src/pages/admin-wording
```

**Expected**: every test passes, including the three named "... even when the form/editor is gone by the time the
save answers". Then the mutation check: in `src/pages/admin-emails/email-editor.tsx`, put the save back to
`changes.save.mutate(draft, { onSuccess: () => toast.success(t('emails.saved')) })` and run again - the email test
fails. The same holds for `wording-editor.tsx` and its test.

**Recorded at the merge**: reverting either editor's fix turned its test red (the pull request). For `ReviewForm`,
the plan and tasks say its test also fails when reverted; the pull request's evidence names only the editors. The
whole storefront suite: 453 of 453 in 81 files.

---

## Scenario 8 - The suite is type-checked with the storefront (research D13)

```bash
cd client
npx tsc -b
npm run lint
```

**Expected**: both clean; `tsc -b` now includes `e2e/` and `playwright.config.ts` through `tsconfig.e2e.json`, and
`npm test` (Vitest) runs no file from `e2e/`.

**Recorded at the merge**: lint and `tsc -b` clean.
