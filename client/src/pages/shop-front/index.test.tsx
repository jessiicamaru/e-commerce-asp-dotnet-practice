import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { ApiError } from '@/config/axios'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { Shops } from '@/services/shops'
import { renderSignedOut } from '@/test/render'
import { ShopFrontPage } from '.'

const lens: ProductModel = {
  id: 'p1', name: 'Fujifilm X-T5', description: null, price: 41000000, currency: 'VND',
  availability: 'InStock', sku: 'FUJI-XT5', categoryId: 'c1', isActive: true, imageUrl: null,
  sellerId: 's1', sellerName: 'Mai Lens', priceVaries: false, variantCount: 1, reviewStatus: 'Approved',
  reviewReason: null, ratingAverage: null, ratingCount: 0, variants: [],
}

function renderShop(path = '/shops/s1') {
  return renderSignedOut(
    <Routes>
      <Route path="/shops/:sellerId" element={<ShopFrontPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopFrontPage', () => {
  /** What the page is for (specs/099): the shop's words, and only that shop's products. */
  it('shows the shop and asks for its products only', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: 'Used Fujifilm bodies.', productCount: 1 })
    const list = vi.spyOn(Product, 'list').mockResolvedValue({
      items: [lens], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false,
    })
    renderShop()

    expect(await screen.findByRole('heading', { name: 'Mai Lens' })).toBeInTheDocument()
    expect(screen.getByText('Used Fujifilm bodies.')).toBeInTheDocument()
    expect(screen.getByText('1 product on sale')).toBeInTheDocument()
    expect(await screen.findByText('Fujifilm X-T5')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith(expect.objectContaining({ sellerId: 's1', pageNumber: 1 }))
  })

  /** The description is the seller's text: shown as text, never as markup. */
  it('shows a description as text', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: '<b>bold</b>', productCount: 0 })
    vi.spyOn(Product, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    renderShop()

    expect(await screen.findByText('<b>bold</b>')).toBeInTheDocument()
  })

  /** A closed or unknown shop is the server's 404 - one message, whichever it was. */
  it('says a closed shop is not open', async () => {
    vi.spyOn(Shops, 'get').mockRejectedValue(new ApiError(404, { title: 'Shop not found.' }))
    vi.spyOn(Product, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    renderShop()

    expect(await screen.findByText('This shop is not open.')).toBeInTheDocument()
  })
})
