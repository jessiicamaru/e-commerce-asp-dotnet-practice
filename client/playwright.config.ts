import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests in a real browser against the real stack (specs/080, #117): the storefront container (nginx on
 * :8088) in front of the gateway and every service, started with docker compose. Nothing here is mocked - that is
 * the point: a gateway route missing or a field renamed fails here and nowhere else.
 *
 * <p>
 * The browser: locally, the Microsoft Edge that Windows already has (`channel: 'msedge'`), so nothing is
 * downloaded; in CI, Playwright's own Chromium (`E2E_BROWSER_CHANNEL=` empty, after `playwright install`). Both
 * are Chromium.
 * </p>
 */
const channel = process.env.E2E_BROWSER_CHANNEL ?? 'msedge'

export default defineConfig({
  testDir: './e2e',
  // One stack, shared state: the flows run in order, and a real checkout is seconds, not milliseconds.
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 20_000 },
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8088',
    // Kept only when something fails - what CI uploads for a person to open in the trace viewer.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'en-US',
  },
  projects: [
    {
      name: 'storefront',
      use: { ...devices['Desktop Chrome'], ...(channel ? { channel } : {}) },
    },
  ],
})
