import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  GUEST_CART_MAX_LINES,
  addToGuestCart,
  clearGuestCart,
  readGuestCart,
  removeFromGuestCart,
  setGuestCartQuantity,
  subscribeToGuestCart,
} from './guest-cart'

beforeEach(() => localStorage.removeItem('guestCart'))
afterEach(() => vi.restoreAllMocks())

describe('the browser\'s cart (specs/162)', () => {
  it('keeps one line per shape, adding again raises it, and survives a reload', () => {
    addToGuestCart('p1', 'black', 1)
    addToGuestCart('p1', 'silver', 2)
    addToGuestCart('p1', 'black', 2)

    // What a reload reads: only what is stored.
    expect(JSON.parse(localStorage.getItem('guestCart')!).lines).toEqual([
      { productId: 'p1', variantId: 'black', quantity: 3 },
      { productId: 'p1', variantId: 'silver', quantity: 2 },
    ])
  })

  it('changes and removes a line by its shape, and empties', () => {
    addToGuestCart('p1', 'black', 1)
    addToGuestCart('p2', 'p2', 1)

    setGuestCartQuantity('black', 4)
    removeFromGuestCart('p2')
    expect(readGuestCart()).toEqual([{ productId: 'p1', variantId: 'black', quantity: 4 }])

    clearGuestCart()
    expect(readGuestCart()).toEqual([])
    expect(localStorage.getItem('guestCart')).toBeNull()
  })

  it('drops what it cannot read rather than send it', () => {
    localStorage.setItem('guestCart', JSON.stringify({ lines: [{ productId: 'p1', variantId: 'v1', quantity: 2 }, { productId: '', quantity: -1 }, 'x'] }))
    expect(readGuestCart()).toEqual([{ productId: 'p1', variantId: 'v1', quantity: 2 }])

    localStorage.setItem('guestCart', '{not json')
    expect(readGuestCart()).toEqual([])
  })

  it('holds at most 50 lines, the server\'s bound', () => {
    for (let i = 0; i < GUEST_CART_MAX_LINES; i++) expect(addToGuestCart(`p${i}`, `v${i}`, 1)).toBe(true)

    expect(addToGuestCart('one-more', 'one-more', 1)).toBe(false)
    // Raising a shape already held still works.
    expect(addToGuestCart('p0', 'v0', 1)).toBe(true)
    expect(readGuestCart()).toHaveLength(GUEST_CART_MAX_LINES)
  })

  it('says so when this browser cannot keep it', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })

    expect(addToGuestCart('p1', 'v1', 1)).toBe(false)
    expect(readGuestCart()).toEqual([])
  })

  it('tells a subscriber about a change', () => {
    const changed = vi.fn()
    const stop = subscribeToGuestCart(changed)

    addToGuestCart('p1', 'v1', 1)
    stop()
    addToGuestCart('p1', 'v1', 1)

    expect(changed).toHaveBeenCalledTimes(1)
  })
})
