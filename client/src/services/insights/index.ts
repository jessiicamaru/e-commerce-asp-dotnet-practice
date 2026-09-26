// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Revenue, SellerProductInsights, TopBuyer, TopProduct, UserBrief, UserStats, ViewedProduct } from './types'

const range = (from: string, to: string) => `from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`

/**
 * An administrator's view of the shop (specs/047), gathered from the three services that know each part:
 * Order (money, what sold, who bought), Catalog (what people look at) and Identity (who people are).
 * Every call is refused by the server for anybody who is not an administrator.
 */
export class Insights {
  static async revenue(from: string, to: string): Promise<Revenue> {
    const { data } = await http.get<Revenue>(`/orders/insights/revenue?${range(from, to)}`)
    return data
  }

  static async topProducts(from: string, to: string, limit: number): Promise<TopProduct[]> {
    const { data } = await http.get<TopProduct[]>(`/orders/insights/top-products?${range(from, to)}&by=units&limit=${limit}`)
    return data
  }

  static async topBuyers(from: string, to: string, currency: string, limit: number): Promise<TopBuyer[]> {
    const { data } = await http.get<TopBuyer[]>(`/orders/insights/top-buyers?${range(from, to)}&currency=${currency}&limit=${limit}`)
    return data
  }

  static async topViewed(from: string, to: string, limit: number): Promise<ViewedProduct[]> {
    const { data } = await http.get<ViewedProduct[]>(`/products/insights/top-viewed?${range(from, to)}&limit=${limit}`)
    return data
  }

  /**
   * The signed-in seller's own revenue (specs/068): their lines only, never an order's total. There is no
   * seller id to send - the token says whose, as with `/orders/sales`.
   */
  static async sellerRevenue(from: string, to: string): Promise<Revenue> {
    const { data } = await http.get<Revenue>(`/orders/sales/insights/revenue?${range(from, to)}`)
    return data
  }

  static async sellerTopProducts(from: string, to: string, limit: number): Promise<TopProduct[]> {
    const { data } = await http.get<TopProduct[]>(`/orders/sales/insights/top-products?${range(from, to)}&limit=${limit}`)
    return data
  }

  /** The signed-in seller's products: views, ratings, the most viewed - from Catalog, which keeps both. */
  static async mine(from: string, to: string, limit: number): Promise<SellerProductInsights> {
    const { data } = await http.get<SellerProductInsights>(`/products/insights/mine?${range(from, to)}&limit=${limit}`)
    return data
  }

  static async userStats(): Promise<UserStats> {
    const { data } = await http.get<UserStats>('/users/stats')
    return data
  }

  /** Who these buyer ids are - Order knows buyers only by id. */
  static async people(ids: string[]): Promise<UserBrief[]> {
    if (ids.length === 0) return []
    const { data } = await http.get<UserBrief[]>(`/users/lookup?${ids.map((id) => `ids=${id}`).join('&')}`)
    return data
  }
}
