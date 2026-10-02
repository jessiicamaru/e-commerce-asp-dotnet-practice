import { describe, expect, it } from 'vitest'
import { queryKeys } from '.'

/**
 * A page of one - read for a count - and a list's page of twelve are different data, so they are different keys
 * (specs/130, #268): under one key the one-row page stood in for the queue's first page.
 */
describe('paged list keys carry the page size (specs/130)', () => {
  it.each([
    ['review queue', (size: number) => queryKeys.reviewQueue('Pending', 1, size)],
    ['shop applications', (size: number) => queryKeys.shopApplications('Pending', 1, size)],
    ['outgoing emails', (size: number) => queryKeys.outgoingEmails('Failed', '', 1, size)],
  ])('%s', (_, key) => {
    expect(key(1)).not.toEqual(key(12))
    // Still under the prefix a decision invalidates.
    expect(key(1)[0]).toEqual(key(12)[0])
  })
})
