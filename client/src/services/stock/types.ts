/** What Inventory reports for one sellable unit — a VARIANT id since specs/020. */
export interface Stock {
  productId: string
  sku: string
  quantityOnHand: number
  /** How many are inside somebody's checkout right now. Not available, not sold. */
  quantityReserved: number
  quantityAvailable: number
}
