import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Moderation } from '@/services/moderation'
import { ShopApplications } from '@/services/shop-applications'
import { renderAsModerator } from '@/test/render'
import { AdminModerationPage, RECENT_DECISIONS } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminModerationPage (specs/045)', () => {
  /** The counts are the queues' own totals, so the dashboard never disagrees with the queue it opens. */
  it('says how much is waiting in each queue and what the moderator decided', async () => {
    const products = vi.spyOn(Moderation, 'products').mockResolvedValue({
      items: [], pageNumber: 1, totalPages: 7, totalCount: 7, hasPreviousPage: false, hasNextPage: true,
    })
    vi.spyOn(ShopApplications, 'list').mockResolvedValue({ items: [], page: 1, pageSize: 1, totalCount: 2 })
    const mine = vi.spyOn(Moderation, 'myDecisions').mockResolvedValue({
      items: [{
        id: 'e1', category: 'Moderation', action: 'ProductApproved', actorId: 'm1', actorEmail: 'mod@example.test',
        actorRole: 'Moderator', subjectType: 'Product', subjectId: 'p1', summary: '"Mai Lens 35mm" approved',
        service: 'catalog', occurredAt: '2026-09-24T08:00:00Z', changeCount: 1,
      }],
      page: 1, pageSize: RECENT_DECISIONS, totalCount: 1,
    })
    renderAsModerator(
      <Routes>
        <Route path="/admin/moderation" element={<AdminModerationPage />} />
      </Routes>,
      '/admin/moderation',
    )

    expect(await screen.findByRole('link', { name: /Products waiting\s*7/ })).toHaveAttribute('href', '/admin/products')
    expect(await screen.findByRole('link', { name: /Shops waiting\s*2/ })).toHaveAttribute('href', '/admin/shops')
    expect(await screen.findByText('Product approved')).toBeInTheDocument()
    expect(products).toHaveBeenCalledWith('Pending', 1, 1)
    expect(mine).toHaveBeenCalledWith(1, RECENT_DECISIONS)
  })
})
