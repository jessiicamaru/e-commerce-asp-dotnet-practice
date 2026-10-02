import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'
import { testSetup, workspaceAliases } from '../../workspace.aliases.ts'

/**
 * The storefront's unit tests: its pages, layouts and components.
 *
 * Separate from any `vite.config.ts` on purpose: that file serves and builds an app, and carries a dev proxy to the
 * gateway. A test must never reach the gateway - the shared setup fails any request that escapes a stub, so a test that
 * quietly hit a real service would be a red test rather than a slow one.
 *
 * What is worth testing is the logic that can be WRONG: what a hook asks the server for, what a guard lets through, what
 * a form sends, and how a server refusal reaches a person. Not that a div rendered.
 */
export default defineConfig({
  plugins: [react()],
  resolve: { alias: workspaceAliases(import.meta.dirname) },
  test: {
    name: 'storefront',
    environment: 'jsdom',
    globals: true,
    setupFiles: [testSetup],
    include: ['src/**/*.test.{ts,tsx}'],
    // 15s, not the default 5s. The tests that type into forms with userEvent - a price, a SKU, a
    // tracking reference - take 3-5s each when twenty files run in parallel on a machine also running
    // the Docker stack, and different ones timed out on different runs ("Test timed out in 5000ms",
    // never an assertion). A real hang still fails; a slow keyboard no longer does.
    testTimeout: 15_000,
  },
})
