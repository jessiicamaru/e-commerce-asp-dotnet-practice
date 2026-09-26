# Tasks: The storefront in a real browser

- [X] T001 Playwright in `client/`: the config (Edge locally, Chromium in CI), the tsconfig, `npm run e2e`, and
  Playwright's output ignored.
- [X] T002 API setup and clean-up (`e2e/support/api.ts`), sign-in and paging helpers (`e2e/support/ui.ts`).
- [X] T003 The four flows (`e2e/flows.spec.ts`), passing three runs in a row against the local stack.
- [X] T004 The lost-toast defect they found: `ReviewForm` and the email and notice editors, each with a unit test
  that fails when reverted.
- [X] T005 The broken-route check (the cart route renamed makes the buying flow fail), the CI job with traces, and
  `publish` gated on it.
- [X] T006 The docs: testing strategy, the client README, CLAUDE.md, counts, timeline and backlog.
