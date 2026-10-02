export type CartLineStatus =
  | 'Available'
  | 'NotForSale'
  | 'NoLongerAvailable'
  | 'PriceUnavailable'
  /** Nobody has priced this shape in the currency being browsed in (specs/022). */
  | 'NotSoldInCurrency'

export interface CartLine {
  productId: string
  /** Which shape of the product this line is (specs/020) - and how it is addressed. */
  variantId: string
  /** That shape in words; empty for a product sold one way. */
  optionSummary: string
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
  /** Which currency the amounts above are in (specs/022). Not frozen - a cart is not a purchase. */
  currency: string
}
