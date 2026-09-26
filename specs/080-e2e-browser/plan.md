# Implementation Plan: The storefront in a real browser

**Branch**: `080-e2e-browser` | **Spec**: [spec.md](spec.md) | **Issue**: #117

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

## Found on the way

The first run found a real defect. **A toast lost when saving remounts its form:**
- the first review gives `ReviewForm` a new key;
- a save, reset or restore gives the email and notice editors a new version.

Each remounts before a callback handed to `mutate()` runs, and TanStack does not run those callbacks for a component
that is gone. The change was saved, but nobody was told. The fix is `mutateAsync().then(...)`.

Each fix has a unit test that unmounts before the save answers, and each test fails when its fix is reverted.

## Research

- **D1 - compose in CI, not `dotnet run`.** The flows need the gateway, the storefront's nginx, Mailpit and SeaweedFS
  as well as the services. Compose is how a person runs them, and it is one command.
- **D2 - data through the API, pages through the browser.** Setting a seller up through five screens would test the
  shop-application screens four times over. The flows click what the issue names, and the rest is made the way
  Bruno makes it.
