import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Insights } from '.'

describe("Insights - a seller's own (specs/068)", () => {
  /** Whose figures these are comes from the token, as with `/orders/sales` (Constitution IV). */
  it('asks for the period and a limit, and names no seller', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })
    const from = '2026-09-01T00:00:00.000Z'
    const to = '2026-09-25T10:00:00.000Z'
    const range = 'from=2026-09-01T00%3A00%3A00.000Z&to=2026-09-25T10%3A00%3A00.000Z'

    await Insights.sellerRevenue(from, to)
    await Insights.sellerTopProducts(from, to, 5)
    await Insights.mine(from, to, 5)

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      `/orders/sales/insights/revenue?${range}`,
      `/orders/sales/insights/top-products?${range}&limit=5`,
      `/products/insights/mine?${range}&limit=5`,
    ])
    expect(get.mock.calls.every(([url]) => !/seller(Id)?=|userId/i.test(url as string))).toBe(true)
  })
})
