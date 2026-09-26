# Feature Specification: The storefront in a real browser

**Feature Branch**: `080-e2e-browser` | **Created**: 2026-09-26 | **Issue**: #117 (closes it)

## Why

The storefront has unit tests (Vitest), and the API has Bruno and `verify-saga.sh`, but nothing clicks through the
real pages against the real stack. A page and the API it calls drifting apart, such as a missing gateway route or a
renamed field, is found by a person.

## User Scenarios

Four flows, in order, each through the real pages:

1. **A moderator approves a product** waiting for review, from `/admin/products`.
2. **A customer buys**: product page, add to cart, cart, checkout, place order. The order settles to *Paid* through
   the saga.
3. **The seller ships** their parcel from `/shop/sales/:id`: start preparing, then mark as shipped with a tracking
   reference.
4. **The customer confirms it arrived and reviews** the product once the right to review arrives.

## Requirements

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

## Acceptance

- The four flows pass. Locally they passed three runs in a row in Edge, and in CI in Chromium.
- A deliberately broken gateway route makes one fail. Verified: the cart route renamed made the buying flow fail.
