import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Insights } from '@/services/insights'
import { Moderation } from '@/services/moderation'
import { ShopApplications } from '@/services/shop-applications'
import { renderAsAdmin } from '@/test/render'
import { AdminOverviewPage } from '.'

function renderPage() {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/overview" element={<AdminOverviewPage />} />
    </Routes>,
    '/admin/overview',
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Insights, 'revenue').mockResolvedValue({
    from: '', to: '',
    totals: [
      { currency: 'VND', revenue: 84_000_000, orders: 2, averageOrderValue: 42_000_000 },
      { currency: 'USD', revenue: 1_700, orders: 1, averageOrderValue: 1_700 },
    ],
    days: [
      { day: '2026-09-20', currency: 'VND', revenue: 84_000_000, orders: 2 },
      { day: '2026-09-21', currency: 'USD', revenue: 1_700, orders: 1 },
    ],
  })
  vi.spyOn(Insights, 'topProducts').mockResolvedValue([{ productId: 'p1', productName: 'Fujifilm X-T5', units: 3, revenue: [] }])
  vi.spyOn(Insights, 'topViewed').mockResolvedValue([{ productId: 'p2', name: 'Sony A7 IV', views: 41 }])
  vi.spyOn(Insights, 'topBuyers').mockResolvedValue([{ customerId: 'c1', orders: 2, spent: [{ currency: 'VND', amount: 84_000_000 }] }])
  vi.spyOn(Insights, 'people').mockResolvedValue([{ id: 'c1', email: 'lan@example.test', firstName: 'Lan', lastName: 'Pham' }])
  vi.spyOn(Insights, 'userStats').mockResolvedValue({ total: 20, customers: 18, sellers: 3, moderators: 1, admins: 1, locked: 1, banned: 1 })
  vi.spyOn(Moderation, 'products').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 4, totalCount: 4, hasPreviousPage: false, hasNextPage: true })
  vi.spyOn(ShopApplications, 'list').mockResolvedValue({ items: [], page: 1, pageSize: 1, totalCount: 2 })
})

describe('AdminOverviewPage (specs/047)', () => {
  /** Dong and dollars are two facts: two totals, never one number made of both. */
  it('shows revenue per currency, never added together', async () => {
    renderPage()

    expect(await screen.findByRole('button', { name: /₫84,000,000\s*2 orders/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /\$1,700\.00\s*1 order/ })).toBeInTheDocument()
    expect(screen.queryByText(/85,700,000|84,001,700/)).not.toBeInTheDocument()
  })

  it('names the top buyers by email and shows what waits for review', async () => {
    renderPage()

    expect(await screen.findByText('lan@example.test')).toBeInTheDocument()
    expect(screen.getByText('Fujifilm X-T5')).toBeInTheDocument()
    expect(screen.getByText('41 views')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Products to review\s*4/ })).toHaveAttribute('href', '/admin/products')
  })

  it('asks again for a different period', async () => {
    const revenue = vi.mocked(Insights.revenue)
    const user = userEvent.setup()
    renderPage()
    await screen.findByRole('button', { name: /₫84,000,000\s*2 orders/ })
    const [firstFrom] = revenue.mock.calls[0]

    await user.click(screen.getByRole('button', { name: 'Last 7 days' }))

    await waitFor(() => expect(revenue).toHaveBeenCalledTimes(2))
    const [secondFrom] = revenue.mock.calls[1]
    expect(new Date(secondFrom).getTime()).toBeGreaterThan(new Date(firstFrom).getTime())
  })
})
