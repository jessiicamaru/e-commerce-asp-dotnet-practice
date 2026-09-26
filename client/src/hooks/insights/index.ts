import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Insights } from '@/services/insights'

/** How many rows each "top" list shows on the Overview. */
export const TOP = 5

/**
 * The Overview's reads, each keyed by the period's start - so choosing a period asks again, and going
 * back to one already seen shows it at once.
 */
export function useInsights(from: string, to: string, currency: string) {
  const revenue = useQuery({ queryKey: queryKeys.insights('revenue', from), queryFn: () => Insights.revenue(from, to) })
  const products = useQuery({ queryKey: queryKeys.insights('products', from), queryFn: () => Insights.topProducts(from, to, TOP) })
  const buyers = useQuery({
    queryKey: [...queryKeys.insights('buyers', from), currency],
    queryFn: () => Insights.topBuyers(from, to, currency, TOP),
  })
  const viewed = useQuery({ queryKey: queryKeys.insights('viewed', from), queryFn: () => Insights.topViewed(from, to, TOP) })
  const stats = useQuery({ queryKey: ['insights', 'people-stats'], queryFn: () => Insights.userStats() })
  const ids = (buyers.data ?? []).map((b) => b.customerId)
  const people = useQuery({ queryKey: queryKeys.people(ids), queryFn: () => Insights.people(ids), enabled: ids.length > 0 })

  return { revenue, products, buyers, viewed, stats, people }
}

/**
 * A seller's own insights (specs/068): two services, each answering from its own data - Order for money and
 * what sold, Catalog for views and ratings - composed here, as the admin's Overview is.
 */
export function useSellerInsights(from: string, to: string) {
  const revenue = useQuery({ queryKey: queryKeys.insights('seller-revenue', from), queryFn: () => Insights.sellerRevenue(from, to) })
  const products = useQuery({ queryKey: queryKeys.insights('seller-products', from), queryFn: () => Insights.sellerTopProducts(from, to, TOP) })
  const mine = useQuery({ queryKey: queryKeys.insights('seller-mine', from), queryFn: () => Insights.mine(from, to, TOP) })

  return { revenue, products, mine }
}
