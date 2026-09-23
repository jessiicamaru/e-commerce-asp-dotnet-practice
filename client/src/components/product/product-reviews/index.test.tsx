import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import type { Product } from '@/services/product/types'
import { Reviews } from '@/services/review'
import type { Review } from '@/services/review/types'
import { refusal } from '@/test/refusal'
import { renderAsSeller } from '@/test/render'
import { ProductReviews } from '.'

const camera: Product = {
  id: 'p1', name: 'Fujifilm X-T5', description: null, price: 42_000_000, currency: 'VND', availability: 'InStock', sku: 'FUJI-XT5',
  categoryId: 'c1', isActive: true, imageUrl: null, sellerId: null, sellerName: null, priceVaries: false, variantCount: 1,
  variants: null, reviewStatus: 'Approved', reviewReason: null, ratingAverage: 4.5, ratingCount: 2,
}
const review = (over: Partial<Review> = {}): Review => ({
  id: 'r1', productId: 'p1', authorName: 'Lan', rating: 5, body: 'Sharp and quiet', createdAt: '2026-09-20T08:00:00Z',
  updatedAt: '2026-09-20T08:00:00Z', edited: false, productName: null, hiddenAt: null, hiddenReason: null, ...over,
})
const page = (...items: Review[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductReviews (specs/046)', () => {
  it('shows the average, the count and what buyers wrote', async () => {
    vi.spyOn(Reviews, 'forProduct').mockResolvedValue(page(review()))
    vi.spyOn(Reviews, 'mine').mockResolvedValue({ eligible: false, review: null })
    renderAsSeller(<ProductReviews product={camera} />)

    expect(await screen.findByText('Sharp and quiet')).toBeInTheDocument()
    expect(screen.getByText('2 reviews')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Rated 4.5 out of 5' })).toBeInTheDocument()
  })

  /** Who may write is the server's call; somebody who did not receive it is told so, and offered no form. */
  it('offers no form to somebody who has not received it', async () => {
    vi.spyOn(Reviews, 'forProduct').mockResolvedValue(page())
    vi.spyOn(Reviews, 'mine').mockResolvedValue({ eligible: false, review: null })
    renderAsSeller(<ProductReviews product={camera} />)

    expect(await screen.findByText('Only customers who have received this product can review it.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Post review' })).not.toBeInTheDocument()
  })

  it('lets somebody who received it post stars and words', async () => {
    vi.spyOn(Reviews, 'forProduct').mockResolvedValue(page())
    vi.spyOn(Reviews, 'mine').mockResolvedValue({ eligible: true, review: null })
    const write = vi.spyOn(Reviews, 'write').mockResolvedValue(review({ rating: 4 }))
    const user = userEvent.setup()
    renderAsSeller(<ProductReviews product={camera} />)

    const post = await screen.findByRole('button', { name: 'Post review' })
    expect(post).toBeDisabled()   // no stars, no review
    await user.click(screen.getByRole('button', { name: '4 stars' }))
    await user.type(screen.getByLabelText('What did you think? (optional)'), 'Good grip')
    await user.click(post)

    await waitFor(() => expect(write).toHaveBeenCalledWith('p1', 4, 'Good grip'))
  })

  /** One review each: theirs fills the form, and saving it again is an update. */
  it('fills the form with their own review to edit', async () => {
    vi.spyOn(Reviews, 'forProduct').mockResolvedValue(page(review()))
    vi.spyOn(Reviews, 'mine').mockResolvedValue({ eligible: true, review: review() })
    renderAsSeller(<ProductReviews product={camera} />)

    expect(await screen.findByRole('button', { name: 'Update review' })).toBeEnabled()
    expect(screen.getByRole('button', { name: '5 stars' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('What did you think? (optional)')).toHaveValue('Sharp and quiet')
  })

  it('shows the server refusal in its words', async () => {
    vi.spyOn(Reviews, 'forProduct').mockResolvedValue(page())
    vi.spyOn(Reviews, 'mine').mockResolvedValue({ eligible: true, review: null })
    vi.spyOn(Reviews, 'write').mockRejectedValue(refusal(403, 'Only a customer who has received this product can review it.'))
    const user = userEvent.setup()
    renderAsSeller(<ProductReviews product={camera} />)

    await user.click(await screen.findByRole('button', { name: '5 stars' }))
    await user.click(screen.getByRole('button', { name: 'Post review' }))

    expect(await screen.findByText('Only a customer who has received this product can review it.')).toBeInTheDocument()
  })
})
