export type CartLineStatus = 'Available' | 'NotForSale' | 'NoLongerAvailable' | 'PriceUnavailable'

export interface CartLine {
  productId: string
  name: string | null
  quantity: number
  unitPrice: number | null
  lineTotal: number | null
  status: CartLineStatus | string
}

export interface Cart {
  lines: CartLine[]
  /** An ESTIMATE: the amount charged is decided at checkout (specs/010). */
  estimatedTotal: number | null
  canCheckOut: boolean
  /** False when Catalog could not be reached. The lines are still there, without prices. */
  pricesAvailable: boolean
}
