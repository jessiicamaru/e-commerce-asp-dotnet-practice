/** How the shop is doing (specs/047). Every amount carries its currency; none is ever summed across them. */
export interface RevenueTotal {
  currency: string
  revenue: number
  orders: number
  averageOrderValue: number
}

export interface RevenueDay {
  day: string
  currency: string
  revenue: number
  orders: number
}

export interface Revenue {
  from: string
  to: string
  totals: RevenueTotal[]
  days: RevenueDay[]
}

export interface CurrencyAmount {
  currency: string
  amount: number
}

export interface TopProduct {
  productId: string
  productName: string
  units: number
  revenue: CurrencyAmount[]
}

export interface TopBuyer {
  customerId: string
  orders: number
  spent: CurrencyAmount[]
}

export interface ViewedProduct {
  productId: string
  name: string
  views: number
}

export interface UserStats {
  total: number
  customers: number
  sellers: number
  moderators: number
  admins: number
  locked: number
  banned: number
}

export interface UserBrief {
  id: string
  email: string
  firstName: string
  lastName: string
}

/** One of a seller's products: views in the period, and its rating over its visible reviews (specs/068). */
export interface SellerProductInsight {
  productId: string
  name: string
  views: number
  ratingAverage: number | null
  ratingCount: number
}

/** A seller's products at a glance: all their views, their rating over every review, the most viewed. */
export interface SellerProductInsights {
  views: number
  /** Weighted by each product's review count; null when nobody has reviewed anything yet. */
  ratingAverage: number | null
  ratingCount: number
  products: SellerProductInsight[]
}

/** A period the Overview offers, in days back from now. */
export const PERIODS = [7, 30, 90] as const
export type Period = (typeof PERIODS)[number]
