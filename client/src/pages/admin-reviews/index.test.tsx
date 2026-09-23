import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Reviews } from '@/services/review'
import type { Review } from '@/services/review/types'
import { renderAsModerator } from '@/test/render'
import { AdminReviewsPage } from '.'

const spam: Review = {
  id: 'r1', productId: 'p1', authorName: 'Lan', rating: 1, body: 'Buy from my site instead', createdAt: '2026-09-20T08:00:00Z',
  updatedAt: '2026-09-20T08:00:00Z', edited: false, productName: 'Fujifilm X-T5', hiddenAt: null, hiddenReason: null,
}
const page = (...items: Review[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false })

function renderPage(path = '/admin/reviews') {
  return renderAsModerator(
    <Routes>
      <Route path="/admin/reviews" element={<AdminReviewsPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminReviewsPage (specs/046)', () => {
  it('hides a review only with a reason', async () => {
    const list = vi.spyOn(Reviews, 'forStaff').mockResolvedValue(page(spam))
    const hide = vi.spyOn(Reviews, 'hide').mockResolvedValue({ ...spam, hiddenAt: '2026-09-24T00:00:00Z' })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('on Fujifilm X-T5')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith(false, 1, PAGE_SIZE)
    await user.click(screen.getByRole('button', { name: 'Hide…' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Hide' })
    expect(confirm).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Reason'), 'Advertising')
    await user.click(confirm)

    await waitFor(() => expect(hide).toHaveBeenCalledWith('r1', 'Advertising'))
  })

  it('shows the hidden ones with why, and puts one back', async () => {
    const list = vi.spyOn(Reviews, 'forStaff').mockResolvedValue(page({ ...spam, hiddenAt: '2026-09-24T00:00:00Z', hiddenReason: 'Advertising' }))
    const restore = vi.spyOn(Reviews, 'restore').mockResolvedValue(spam)
    const user = userEvent.setup()
    renderPage('/admin/reviews?tab=hidden')

    expect(await screen.findByText('Hidden: Advertising')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith(true, 1, PAGE_SIZE)
    await user.click(screen.getByRole('button', { name: 'Show again' }))

    await waitFor(() => expect(restore).toHaveBeenCalledWith('r1'))
  })
})
