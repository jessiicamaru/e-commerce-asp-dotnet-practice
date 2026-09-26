import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Insights } from '@/services/insights'
import type { Revenue } from '@/services/insights/types'
import { renderAsSeller } from '@/test/render'
import { ShopInsightsPage } from '.'

const revenue: Revenue = {
  from: '', to: '',
  totals: [
    { currency: 'VND', revenue: 6_000_000, orders: 3, averageOrderValue: 2_000_000 },
    { currency: 'USD', revenue: 240, orders: 1, averageOrderValue: 240 },
  ],
  days: [{ day: new Date().toISOString().slice(0, 10), currency: 'VND', revenue: 6_000_000, orders: 3 }],
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Insights, 'sellerRevenue').mockResolvedValue(revenue)
  vi.spyOn(Insights, 'sellerTopProducts').mockResolvedValue([
    { productId: 'p1', productName: 'Viltrox 56mm', units: 4, revenue: [{ currency: 'VND', amount: 6_000_000 }, { currency: 'USD', amount: 240 }] },
  ])
  vi.spyOn(Insights, 'mine').mockResolvedValue({
    views: 1_234, ratingAverage: 4.25, ratingCount: 8,
    products: [
      { productId: 'p1', name: 'Viltrox 56mm', views: 900, ratingAverage: 4.5, ratingCount: 6 },
      { productId: 'p2', name: 'Lens cap', views: 334, ratingAverage: null, ratingCount: 0 },
    ],
  })
})

describe('ShopInsightsPage (specs/068)', () => {
  it("asks both services for the seller's own figures over the last 30 days", async () => {
    renderAsSeller(<ShopInsightsPage />, '/shop/insights')

    expect(await screen.findByText('1,234')).toBeInTheDocument()
    const [from, to] = vi.mocked(Insights.sellerRevenue).mock.calls[0]
    // Today and the 29 days before it: whole days, the chart's days (specs/055).
    expect(Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000)).toBe(29)
    expect(Insights.sellerTopProducts).toHaveBeenCalledWith(from, to, 5)
    expect(Insights.mine).toHaveBeenCalledWith(from, to, 5)
  })

  /** Dong and dollars are two facts: two cards, never one number made of both. */
  it('shows revenue per currency, never added together', async () => {
    renderAsSeller(<ShopInsightsPage />, '/shop/insights')

    expect(await screen.findByRole('button', { name: /₫6,000,000\s*3 orders/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /\$240\.00\s*1 order/ })).toBeInTheDocument()
    expect(screen.getByText(/4 sold · ₫6,000,000 · \$240\.00/)).toBeInTheDocument()
  })

  it('shows the rating over every review, and each product with its views and rating', async () => {
    renderAsSeller(<ShopInsightsPage />, '/shop/insights')

    expect(await screen.findByText('4.3')).toBeInTheDocument()
    expect(screen.getByText('8 reviews')).toBeInTheDocument()
    expect(screen.getByText('900 views · ★ 4.5 (6)')).toBeInTheDocument()
    expect(screen.getByText('334 views')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Viltrox 56mm' })[0]).toHaveAttribute('href', '/shop/products/p1')
  })

  it('says there is nothing yet rather than drawing zeros', async () => {
    vi.mocked(Insights.sellerRevenue).mockResolvedValue({ from: '', to: '', totals: [], days: [] })
    vi.mocked(Insights.mine).mockResolvedValue({ views: 0, ratingAverage: null, ratingCount: 0, products: [] })
    vi.mocked(Insights.sellerTopProducts).mockResolvedValue([])
    renderAsSeller(<ShopInsightsPage />, '/shop/insights')

    expect(await screen.findByText('No sales in this period.')).toBeInTheDocument()
    expect(screen.getByText('No reviews yet')).toBeInTheDocument()
    expect(screen.getAllByText('Nothing yet.')).toHaveLength(2)
  })

  it('asks again for a period chosen', async () => {
    const user = userEvent.setup()
    renderAsSeller(<ShopInsightsPage />, '/shop/insights')
    await screen.findByText('1,234')

    await user.click(screen.getByRole('button', { name: 'Last 7 days' }))

    await waitFor(() => expect(Insights.sellerRevenue).toHaveBeenCalledTimes(2))
    const [from, to] = vi.mocked(Insights.sellerRevenue).mock.calls[1]
    expect(Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000)).toBe(6)
  })
})
