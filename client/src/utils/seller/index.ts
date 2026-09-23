import type { SaleSummary } from '@/services/order/types'

/**
 * What a page of sales adds up to, **per currency**.
 *
 * Never one number: an order paid in dollars and one paid in dong do not add up to anything, and this
 * shop converts nothing (specs/022). `{ VND: 9_270_000, USD: 54.99 }` is the honest answer.
 *
 * Every figure here is over the seller's OWN lines - that is what a sale summary carries (specs/034) -
 * so this is what they sold, not what their customers spent in total.
 */
export function summariseSales(sales: SaleSummary[]) {
  const revenue: Record<string, number> = {}
  let units = 0

  for (const sale of sales) {
    const currency = sale.currency || 'VND'
    // Summed in the currency's smallest unit and divided back, so 19.99 + 29.99 is 49.98 and not
    // 49.980000000000004 - which a person would see, because this is printed.
    revenue[currency] = Math.round(((revenue[currency] ?? 0) + sale.subtotal) * 100) / 100
    units += sale.units
  }

  return { orders: sales.length, units, revenue }
}
