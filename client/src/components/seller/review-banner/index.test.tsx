import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { renderAsSeller } from '@/test/render'
import { ReviewBanner } from '.'

const listing = (over: Partial<ProductModel>): ProductModel => ({
  id: 'p1', name: 'Mai Lens 35mm', description: null, price: 9_000_000, currency: 'VND', availability: 'InStock', sku: 'MAI-35',
  categoryId: 'c1', isActive: true, imageUrl: null, sellerId: 'u1', sellerName: 'Mai Lens', priceVaries: false, variantCount: 1,
  variants: null, reviewStatus: 'Approved', reviewReason: null, ratingAverage: null, ratingCount: 0, ...over,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ReviewBanner (specs/045)', () => {
  it('says a waiting listing is not on sale yet', () => {
    renderAsSeller(<ReviewBanner product={listing({ reviewStatus: 'Pending' })} />)

    expect(screen.getByText('Waiting for a moderator')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send back for review' })).not.toBeInTheDocument()
  })

  it('shows why it was rejected and sends it back', async () => {
    const resubmit = vi.spyOn(Product, 'resubmit').mockResolvedValue(listing({ reviewStatus: 'Pending' }))
    renderAsSeller(<ReviewBanner product={listing({ reviewStatus: 'Rejected', reviewReason: 'Photograph the real lens' })} />)

    expect(screen.getByText(/Reason: Photograph the real lens/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Send back for review' }))

    await waitFor(() => expect(resubmit).toHaveBeenCalledWith('p1'))
  })

  /** The one thing a seller would not guess: changing the words or the pictures takes it off the shelf. */
  it('warns an approved listing that its words and photographs are reviewed again', () => {
    renderAsSeller(<ReviewBanner product={listing({})} />)

    expect(screen.getByText(/sends it back for review; prices and stock do not/)).toBeInTheDocument()
  })
})
