import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Cart } from '@/services/cart'
import type { Cart as CartModel } from '@/services/cart/types'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { renderAsSeller } from '@/test/render'
import { CartPage } from '.'

const cart: CartModel = {
  currency: 'VND',
  estimatedTotal: 15990000,
  canCheckOut: true,
  pricesAvailable: true,
  lines: [
    {
      productId: 'p1', variantId: 'v2', optionSummary: 'Mount: Fujifilm X', name: 'Sigma 18-50mm',
      quantity: 1, unitPrice: 15990000, lineTotal: 15990000, status: 'Available',
    },
  ],
}

const sigma = {
  id: 'p1', name: 'Sigma 18-50mm', description: null, price: 15490000, currency: 'VND', availability: 'InStock',
  sku: 'SIGMA', categoryId: 'c', isActive: true, imageUrl: '/img/product.png', sellerId: 's', sellerName: 'Mai',
  priceVaries: true, variantCount: 2, reviewStatus: 'Approved', reviewReason: null, ratingAverage: null, ratingCount: 0,
  variants: [
    { id: 'p1', sku: 'SIGMA-E', price: 15490000, currency: 'VND', optionSummary: 'Mount: Sony E', options: [], availability: 'InStock', isActive: true, imageUrl: '/img/product.png' },
    { id: 'v2', sku: 'SIGMA-X', price: 15990000, currency: 'VND', optionSummary: 'Mount: Fujifilm X', options: [], availability: 'InStock', isActive: true, imageUrl: '/img/fuji-mount.png' },
  ],
} satisfies ProductModel

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Cart, 'get').mockResolvedValue(cart)
  vi.spyOn(Product, 'get').mockResolvedValue(sigma)
})

describe('CartPage', () => {
  /**
   * Somebody who does not follow cameras will not remember which "Sigma 18-50" they added. The
   * picture is the chosen VARIANT's - the Fujifilm-mount one here, not the product's first photo.
   */
  it('shows the picture of the variant in the cart', async () => {
    renderAsSeller(<CartPage />, '/cart')

    // Longer than findBy's default second: the picture needs a SECOND request (the product) after the
    // cart, and under a full parallel run that took 2s - a timing failure, not a wrong picture.
    const picture = await screen.findByAltText('Sigma 18-50mm', {}, { timeout: 5000 })
    await waitFor(() => expect(picture).toHaveAttribute('src', '/img/fuji-mount.png'))
  })

  it('changes the quantity with the plus, sending the variant it belongs to', async () => {
    const setQuantity = vi.spyOn(Cart, 'setQuantity').mockResolvedValue()
    renderAsSeller(<CartPage />, '/cart')

    const line = (await screen.findByRole('link', { name: 'Sigma 18-50mm' })).closest('li') as HTMLElement
    fireEvent.click(within(line).getByRole('button', { name: 'Increase quantity' }))

    await waitFor(() => expect(setQuantity).toHaveBeenCalledWith('v2', 2))
  })

  it('removes a line by its variant', async () => {
    const remove = vi.spyOn(Cart, 'removeItem').mockResolvedValue()
    renderAsSeller(<CartPage />, '/cart')

    fireEvent.click(await screen.findByRole('button', { name: /Remove/ }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('v2'))
  })
})
