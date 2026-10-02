import { defineConfig } from 'vitest/config'

/**
 * Every workspace's unit tests in one run (specs/135): `npm test` from client/. Each project keeps its own config, so a
 * package's tests are run with that package's aliases - in particular without "@/", which only an app has.
 */
export default defineConfig({
  test: {
    projects: ['apps/*/vitest.config.ts', 'packages/*/vitest.config.ts'],
  },
})
