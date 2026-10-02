import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'
import { testSetup, workspaceAliases } from '../../workspace.aliases.ts'

/**
 * The back office's unit tests (specs/136): who it lets in, and its sign-in. Separate from `vite.config.ts` for the
 * same reason as the storefront's - a test never reaches the gateway; the shared setup fails one that tries.
 */
export default defineConfig({
  plugins: [react()],
  resolve: { alias: workspaceAliases(import.meta.dirname) },
  test: {
    name: 'back-office',
    environment: 'jsdom',
    globals: true,
    setupFiles: [testSetup],
    include: ['src/**/*.test.{ts,tsx}'],
    testTimeout: 15_000,
  },
})
