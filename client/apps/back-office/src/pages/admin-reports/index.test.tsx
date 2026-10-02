import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Moderation } from '@ecommerce/core/services/moderation'
import type { Product } from '@ecommerce/core/services/product/types'
import { Reports } from '@ecommerce/core/services/reports'
import type { ReportedItem } from '@ecommerce/core/services/reports/types'
import { Reviews } from '@ecommerce/core/services/review'
import type { Review } from '@ecommerce/core/services/review/types'
import { renderAsModerator } from '@ecommerce/core/test/render'
import { AdminReportsPage } from '.'

const item = (over: Partial<ReportedItem>): ReportedItem => ({
  targetType: 'Review', targetId: 'r1', productId: 'p1', productName: 'Fujifilm X-T5', excerpt: 'Buy it cheaper elsewhere',
  reportCount: 2, reasons: { Spam: 1, Misleading: 1 }, details: ['Links to another shop'],
  firstReportedAt: '2026-09-27T01:00:00Z', lastReportedAt: '2026-09-27T02:00:00Z', ...over,
})

const page = (...items: ReportedItem[]) => ({
  items, pageNumber: 1, totalPages: 1, totalCount: items.length, hasPreviousPage: false, hasNextPage: false,
})

function renderPage() {
  return renderAsModerator(
    <Routes>
      <Route path="/reports" element={<AdminReportsPage />} />
    </Routes>,
    '/reports',
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminReportsPage (specs/101)', () => {
  it('shows each reported thing with its count, reasons and the reporters’ words', async () => {
    vi.spyOn(Reports, 'queue').mockResolvedValue(page(item({})))
    renderPage()

    const card = (await screen.findByText('2 reports')).closest('li')!
    expect(within(card).getByText('Buy it cheaper elsewhere')).toBeInTheDocument()
    expect(within(card).getByText('Spam × 1')).toBeInTheDocument()
    expect(within(card).getByText('“Links to another shop”')).toBeInTheDocument()
    expect(within(card).getByRole('link', { name: 'on “Fujifilm X-T5”' })).toHaveAttribute('href', '/products/p1')
  })

  /** Acting is the existing hide, with a reason - the server closes the reports with it. */
  it('hides a reported review with a reason', async () => {
    vi.spyOn(Reports, 'queue').mockResolvedValue(page(item({})))
    const hide = vi.spyOn(Reviews, 'hide').mockResolvedValue({} as Review)
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Hide review' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Reason'), 'Advertising')
    await user.click(within(dialog).getByRole('button', { name: 'Hide review' }))

    await waitFor(() => expect(hide).toHaveBeenCalledWith('r1', 'Advertising'))
  })

  it('takes a reported product down, and dismisses without acting', async () => {
    vi.spyOn(Reports, 'queue').mockResolvedValue(page(item({ targetType: 'Product', targetId: 'p1', excerpt: 'Fujifilm X-T5' })))
    const takeDown = vi.spyOn(Moderation, 'takeDown').mockResolvedValue({} as Product)
    const dismiss = vi.spyOn(Reports, 'dismiss').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Take product down' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Reason'), 'Counterfeit')
    await user.click(within(dialog).getByRole('button', { name: 'Take product down' }))
    await waitFor(() => expect(takeDown).toHaveBeenCalledWith('p1', 'Counterfeit'))

    await user.click(screen.getByRole('button', { name: 'No action' }))
    await waitFor(() => expect(dismiss).toHaveBeenCalledWith('Product', 'p1'))
  })

  it('says when nothing is waiting', async () => {
    vi.spyOn(Reports, 'queue').mockResolvedValue(page())
    renderPage()
    expect(await screen.findByText(/no open reports/)).toBeInTheDocument()
  })
})
