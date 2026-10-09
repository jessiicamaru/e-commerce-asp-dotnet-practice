import { waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Cart } from '@ecommerce/core/services/cart'
import { renderAsCustomer, renderSignedOut } from '@ecommerce/core/test/render'
import { addToGuestCart, readGuestCart } from '@ecommerce/core/utils/cart/guest-cart'
import { GuestCartMerge } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
  localStorage.removeItem('guestCart')
})

describe('GuestCartMerge (specs/162)', () => {
  it('moves this browser\'s cart into the account\'s once signed in, then empties it', async () => {
    addToGuestCart('p1', 'v1', 2)
    const merge = vi.spyOn(Cart, 'merge').mockResolvedValue()
    vi.spyOn(Cart, 'get').mockResolvedValue({ lines: [], estimatedTotal: 0, canCheckOut: false, pricesAvailable: true, currency: 'VND' })
    renderAsCustomer(<GuestCartMerge />)

    await waitFor(() => expect(merge).toHaveBeenCalledWith([{ productId: 'p1', variantId: 'v1', quantity: 2 }]))
    await waitFor(() => expect(readGuestCart()).toEqual([]))
    expect(merge).toHaveBeenCalledTimes(1)
  })

  it('keeps the browser\'s cart when the merge fails, for the next try', async () => {
    addToGuestCart('p1', 'v1', 2)
    const merge = vi.spyOn(Cart, 'merge').mockRejectedValue(new Error('offline'))
    renderAsCustomer(<GuestCartMerge />)

    await waitFor(() => expect(merge).toHaveBeenCalled())
    expect(readGuestCart()).toEqual([{ productId: 'p1', variantId: 'v1', quantity: 2 }])
  })

  it('does nothing signed out, or with nothing to move', async () => {
    const merge = vi.spyOn(Cart, 'merge').mockResolvedValue()
    addToGuestCart('p1', 'v1', 1)
    const { unmount } = renderSignedOut(<GuestCartMerge />)
    unmount()
    localStorage.removeItem('guestCart')
    renderAsCustomer(<GuestCartMerge />)

    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(merge).not.toHaveBeenCalled()
  })
})
