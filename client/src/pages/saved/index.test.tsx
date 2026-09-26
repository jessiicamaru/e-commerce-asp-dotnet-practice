import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import type { Product } from '@/services/product/types'
import { SavedProduct } from '@/services/saved-product'
import { renderAsCustomer } from '@/test/render'
import { SavedPage } from '.'

const product = (id: string, name: string): Product => ({
  id, name, description: null, price: 9_000_000, currency: 'VND', availability: 'InStock',
  sku: id, categoryId: 'c1', isActive: true, imageUrl: null, sellerId: null, sellerName: null, priceVaries: false,
  variantCount: 1, variants: null, reviewStatus: 'Approved', reviewReason: null, ratingAverage: null, ratingCount: 0,
} as Product)

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(SavedProduct, 'ids').mockResolvedValue(['p1', 'p2'])
})

describe('SavedPage (specs/075)', () => {
  it('lists what was saved and says which can no longer be bought', async () => {
    vi.spyOn(SavedProduct, 'list').mockResolvedValue({
      items: [
        { product: product('p1', 'Ricoh GR IIIx'), savedAt: '2026-09-26T00:00:00Z', available: true },
        { product: product('p2', 'Old lens'), savedAt: '2026-09-25T00:00:00Z', available: false },
      ],
      pageNumber: 1, totalPages: 1, totalCount: 2,
    })
    renderAsCustomer(<SavedPage />, '/saved')

    expect(await screen.findByText('Ricoh GR IIIx')).toBeInTheDocument()
    expect(screen.getByText('Old lens')).toBeInTheDocument()
    expect(screen.getAllByText('No longer available')).toHaveLength(1)
    expect(SavedProduct.list).toHaveBeenCalledWith(1, 12)
    expect(await screen.findAllByRole('button', { name: 'Remove from saved' })).toHaveLength(2)
  })

  it('says how to save something when nothing is saved', async () => {
    vi.spyOn(SavedProduct, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0 })
    renderAsCustomer(<SavedPage />, '/saved')

    expect(await screen.findByText(/Nothing saved yet/)).toBeInTheDocument()
  })
})
