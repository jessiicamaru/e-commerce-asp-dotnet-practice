import path from 'node:path'

const client = import.meta.dirname

/**
 * The import names every app and package agrees on (specs/135, ADR-003), for Vite and Vitest. TypeScript reads the same
 * names from tsconfig.base.json.
 *
 * - `@ecommerce/core/...` and `@ecommerce/ui/...` are the shared packages, as source: the app importing them bundles them.
 * - `cn` is the kit's class helper. shadcn's generated components import it by that bare name, so a component added
 *   with `shadcn add` needs no editing.
 * - `@/...` is the app's own `src/`, and only an app has one: a package that used it would resolve into whichever app
 *   happened to compile it.
 */
export function workspaceAliases(appDir?: string): Record<string, string> {
  return {
    ...(appDir ? { '@/': path.resolve(appDir, 'src') + '/' } : {}),
    '@ecommerce/core/': path.resolve(client, 'packages/core/src') + '/',
    '@ecommerce/ui/': path.resolve(client, 'packages/ui/src') + '/',
    cn: path.resolve(client, 'packages/ui/src/cn.ts'),
  }
}

/** The one test setup every workspace runs: it fails any test that reaches the network. */
export const testSetup = path.resolve(client, 'packages/core/src/test/setup.ts')
