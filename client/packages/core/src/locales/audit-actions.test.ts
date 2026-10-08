/// <reference types="node" />
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import en from './en/admin.json'
import vi from './vi/admin.json'

/**
 * Every action a service records in the audit log has words in both languages (specs/121, #244).
 *
 * <p>
 * The actions are string literals at the call sites of `IAuditTrail.RecordAsync` - about a hundred, in six services -
 * and there is no list of them to compare with. 54 had gone unlabelled and showed on Moderation and the audit log as
 * `ShopClosed`, `TrackingCorrected`... So this reads the server's source: the second argument of each
 * `audit.RecordAsync(` call. A literal, or a ternary between literals, is read directly; a call that passes a variable
 * must be declared in {@link INDIRECT} with the actions it records, so a new one fails here until somebody says.
 * </p>
 */

const SERVER = resolve(import.meta.dirname, '../../../../../server/src')

/** Calls whose action is a variable, by file: what they record, each checked to be written in that file. */
const INDIRECT: Record<string, { actions: string[]; written?: string }> = {
  // The trail itself: its own signature and the overload forwarding to it.
  'BuildingBlocks/Ecommerce.Shared/Audit/AuditTrail.cs': { actions: [] },
  'Services/Activity/Ecommerce.Activity.Application/Notifications/NotificationWordingFeatures.cs': {
    actions: ['NotificationWordingSaved', 'NotificationWordingReset', 'NotificationWordingRestored'],
  },
  'Services/Catalog/Ecommerce.Catalog.Application/Products/Review/ProductReviewFeatures.cs': {
    actions: ['ProductApproved', 'ProductRejected', 'ProductTakenDown'],
  },
  // Through its own RecordAsync(action, ...) (specs/159): each handler passes a literal.
  'Services/Catalog/Ecommerce.Catalog.Application/Specifications/SpecificationCommands.cs': {
    actions: [
      'SpecificationCreated', 'SpecificationRenamed', 'SpecificationTranslated', 'SpecificationDeleted',
      'SpecificationOptionAdded', 'SpecificationOptionRenamed', 'SpecificationOptionTranslated', 'SpecificationOptionDeleted',
    ],
  },
  'Services/Catalog/Ecommerce.Catalog.Application/Sellers/ShopClosure.cs': {
    actions: ['ShopPaused', 'ShopResumed', 'ShopClosed', 'ShopReopened'],
  },
  'Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplateEditing.cs': {
    actions: ['EmailTemplateSaved', 'EmailTemplateReset', 'EmailTemplateRestored'],
  },
  // Through its own AuditAsync(row, action, ...): four literals, and `Return{to}` for a decision.
  'Services/Order/Ecommerce.Order.Application/Returns/ReturnFeatures.cs': {
    actions: ['ReturnRequested', 'ReturnEscalated', 'ReturnSentBack', 'ReturnReceived', 'ReturnAccepted', 'ReturnRefused', 'ReturnRejected'],
    written: '$"Return{to}"',
  },
  // Its helper's signature: the action is the ternary in the same file, read as literals.
  'Services/Order/Ecommerce.Order.Application/Orders/Common/ParcelAudit.cs': { actions: [] },
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return name === 'bin' || name === 'obj' ? [] : sourceFiles(path)
    return name.endsWith('.cs') ? [path] : []
  })
}

/** The top-level arguments of the call whose opening parenthesis ends at `start`. */
function argumentsAt(source: string, start: number): string[] {
  const args: string[] = []
  let depth = 1
  let current = ''
  let inString = false
  for (let i = start; i < source.length && depth > 0; i++) {
    const c = source[i]
    if (c === '"' && source[i - 1] !== '\\') inString = !inString
    if (!inString) {
      if ('([{'.includes(c)) depth++
      else if (')]}'.includes(c)) depth--
      if (c === ',' && depth === 1) {
        args.push(current.trim())
        current = ''
        continue
      }
    }
    if (depth > 0) current += c
  }
  args.push(current.trim())
  return args
}

function recordedActions() {
  const actions = new Set<string>()
  const undeclared: string[] = []
  for (const path of sourceFiles(SERVER)) {
    const file = relative(SERVER, path).replaceAll('\\', '/')
    const source = readFileSync(path, 'utf8')
    for (const call of source.matchAll(/\b_?audit\s*\.\s*RecordAsync\(|\bTask RecordAsync\(/gi)) {
      if (call[0].startsWith('Task')) continue
      const action = argumentsAt(source, call.index + call[0].length)[1] ?? ''
      const literals = [...action.matchAll(/"([A-Za-z]+)"/g)].map((m) => m[1])
      if (literals.length > 0 && !/\$"/.test(action)) literals.forEach((name) => actions.add(name))
      else if (!(file in INDIRECT)) undeclared.push(`${file}: ${action}`)
    }
  }
  for (const [file, { actions: declared, written }] of Object.entries(INDIRECT)) {
    const source = readFileSync(join(SERVER, file), 'utf8')
    for (const name of declared) {
      if (!source.includes(`"${name}"`)) expect(source, `${file} records ${name}`).toContain(written ?? `"${name}"`)
      actions.add(name)
    }
  }
  return { actions, undeclared }
}

describe('audit action labels (specs/121, #244)', () => {
  const { actions, undeclared } = recordedActions()

  it('reads the server: a hundred-odd actions, every indirect call declared', () => {
    expect(undeclared, 'a call passing its action in a variable - declare it in INDIRECT').toEqual([])
    expect(actions.size).toBeGreaterThan(100)
    expect(actions).toContain('ShopClosed')
    expect(actions).toContain('ParcelShipped')
  })

  it.each([
    ['en', en.audit.action],
    ['vi', vi.audit.action],
  ])('has words in %s for every action a service records', (_, labels) => {
    const missing = [...actions].filter((name) => !(name in labels)).sort()
    expect(missing).toEqual([])
  })
})
