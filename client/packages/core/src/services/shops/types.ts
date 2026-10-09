/** A shop as a shopper sees it (specs/099): Catalog's copy of its name and description, and how much is on sale. */
export interface ShopFront {
  sellerId: string
  shopName: string
  description: string | null
  productCount: number
  /** Its seller is away (specs/107): the page still answers, with nothing on the shelf. */
  paused: boolean
  /** Every visible review of its products, averaged (specs/165) - null with none; the seller's insights show the same. */
  ratingAverage: number | null
  ratingCount: number
}

/** Worded by precedence: a banned seller's shop is Suspended whatever else is true (specs/107). */
export type ShopStateName = 'Open' | 'Paused' | 'Closed' | 'Suspended'

/** A shop's state as its seller and staff read it (specs/107) - both dates, so closed-and-paused shows both. */
export interface ShopState {
  sellerId: string
  shopName: string
  state: ShopStateName
  pausedAt: string | null
  closedAt: string | null
  closedReason: string | null
}
