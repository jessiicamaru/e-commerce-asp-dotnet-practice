import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RECENTLY_VIEWED_MAX, readRecentlyViewed, rememberViewed, subscribeToRecentlyViewed } from './recently-viewed'

beforeEach(() => localStorage.removeItem('recentlyViewed'))
afterEach(() => vi.restoreAllMocks())

describe('recently viewed (specs/163)', () => {
  it('keeps the most recent first, each once', () => {
    rememberViewed('a')
    rememberViewed('b')
    rememberViewed('c')
    rememberViewed('a')

    expect(readRecentlyViewed()).toEqual(['a', 'c', 'b'])
  })

  it('keeps at most twelve', () => {
    for (let i = 0; i < RECENTLY_VIEWED_MAX + 3; i++) rememberViewed(`p${i}`)

    expect(readRecentlyViewed()).toHaveLength(RECENTLY_VIEWED_MAX)
    expect(readRecentlyViewed()[0]).toBe(`p${RECENTLY_VIEWED_MAX + 2}`)
  })

  it('reads nothing from what it cannot read, and forgets nothing it could not write', () => {
    localStorage.setItem('recentlyViewed', '{nope')
    expect(readRecentlyViewed()).toEqual([])

    localStorage.setItem('recentlyViewed', JSON.stringify(['a', 7, '', 'a', 'b']))
    expect(readRecentlyViewed()).toEqual(['a', 'b'])

    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })
    expect(() => rememberViewed('c')).not.toThrow()
  })

  it('tells a subscriber', () => {
    const changed = vi.fn()
    const stop = subscribeToRecentlyViewed(changed)
    rememberViewed('a')
    stop()
    rememberViewed('b')

    expect(changed).toHaveBeenCalledTimes(1)
  })
})
