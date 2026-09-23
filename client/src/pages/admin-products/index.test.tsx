import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Moderation } from '@/services/moderation'
import type { Product } from '@/services/product/types'
import { refusal } from '@/test/refusal'
import { renderAsModerator } from '@/test/render'
import { AdminProductsPage } from '.'

const lens = (over: Partial<Product> = {}): Product => ({
  id: 'p1', name: 'Mai Lens 35mm', description: 'A fast prime', price: 9_000_000, currency: 'VND', availability: 'OutOfStock',
  sku: 'MAI-35', categoryId: 'c1', isActive: true, imageUrl: null, sellerId: 's1', sellerName: 'Mai Lens', priceVaries: false,
  variantCount: 1, variants: null, reviewStatus: 'Pending', reviewReason: null, ratingAverage: null, ratingCount: 0, ...over,
})
const page = (...items: Product[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

function renderPage(path = '/admin/products') {
  return renderAsModerator(
    <Routes>
      <Route path="/admin/products" element={<AdminProductsPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminProductsPage (specs/045)', () => {
  it('opens on what is waiting, naming the shop', async () => {
    const list = vi.spyOn(Moderation, 'products').mockResolvedValue(page(lens()))
    renderPage()

    expect(await screen.findByText('Mai Lens 35mm')).toBeInTheDocument()
    expect(screen.getByText(/by Mai Lens/)).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith('Pending', 1, PAGE_SIZE)
  })

  it('approves at a press', async () => {
    vi.spyOn(Moderation, 'products').mockResolvedValue(page(lens()))
    const approve = vi.spyOn(Moderation, 'approve').mockResolvedValue(lens({ reviewStatus: 'Approved' }))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(approve).toHaveBeenCalledWith('p1'))
  })

  /** The seller reads the reason, so there is no rejecting without one. */
  it('rejects only with a reason', async () => {
    vi.spyOn(Moderation, 'products').mockResolvedValue(page(lens()))
    const reject = vi.spyOn(Moderation, 'reject').mockResolvedValue(lens({ reviewStatus: 'Rejected' }))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Reject…' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Reject' })
    expect(confirm).toBeDisabled()

    await user.type(within(dialog).getByLabelText('Reason'), 'Photograph the real lens')
    await user.click(confirm)

    await waitFor(() => expect(reject).toHaveBeenCalledWith('p1', 'Photograph the real lens'))
  })

  it('takes down what is on sale, with a reason, and offers no approval there', async () => {
    vi.spyOn(Moderation, 'products').mockResolvedValue(page(lens({ reviewStatus: 'Approved' })))
    const takeDown = vi.spyOn(Moderation, 'takeDown').mockResolvedValue(lens({ reviewStatus: 'Rejected' }))
    const user = userEvent.setup()
    renderPage('/admin/products?status=Approved')

    await user.click(await screen.findByRole('button', { name: 'Take down…' }))
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Reason'), 'Counterfeit')
    await user.click(within(dialog).getByRole('button', { name: 'Take down' }))

    await waitFor(() => expect(takeDown).toHaveBeenCalledWith('p1', 'Counterfeit'))
  })

  it('shows somebody else deciding first as the server says it', async () => {
    vi.spyOn(Moderation, 'products').mockResolvedValue(page(lens()))
    vi.spyOn(Moderation, 'approve').mockRejectedValue(refusal(409, 'This product is approved; nothing to do.'))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Approve' }))

    expect(await screen.findByText('This product is approved; nothing to do.')).toBeInTheDocument()
  })
})
