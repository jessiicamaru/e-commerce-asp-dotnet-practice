/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * The client's one layering rule (specs/135, ADR-003): a shared package never imports an app, and the kit never
 * imports the core. An app is where pages live; the moment a package reaches into one, a second app (the back office)
 * either cannot build or silently gets the first app's code - `@/` would resolve into whichever app compiled it.
 *
 * Read from the source rather than from a type-checker: a package importing an app may well compile, which is the
 * problem.
 */
const PACKAGES = resolve(import.meta.dirname, '../../..')
const IMPORT = /(?:from\s+|import\s*\(\s*|import\s+|vi\.mock\(\s*)['"]([^'"]+)['"]/g

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return name === 'node_modules' ? [] : sources(path)
    return /\.(ts|tsx)$/.test(name) ? [path] : []
  })
}

/** Every import in a package that breaks the rule, as "file: specifier". */
function violations(pkg: 'core' | 'ui', root = join(PACKAGES, pkg, 'src')): string[] {
  return sources(root).flatMap((file) =>
    [...readFileSync(file, 'utf-8').matchAll(IMPORT)]
      .map((m) => m[1])
      .filter((spec) => {
        if (spec.startsWith('@/')) return true // an app's own folders
        if (spec.startsWith('.')) {
          // a relative path may leave the package only to read the server's declarations (tests do)
          const target = resolve(dirname(file), spec)
          return !target.startsWith(root) && !/[\\/]server[\\/]/.test(target)
        }
        return pkg === 'ui' && spec.startsWith('@ecommerce/core')
      })
      .map((spec) => `${relative(PACKAGES, file)}: ${spec}`),
  )
}

describe('the shared packages never import an app (specs/135)', () => {
  it('core imports no app', () => {
    expect(violations('core')).toEqual([])
  })

  it('the kit imports neither an app nor the core', () => {
    expect(violations('ui')).toEqual([])
  })
})
