/** A shop as a shopper sees it (specs/099): Catalog's copy of its name and description, and how much is on sale. */
export interface ShopFront {
  sellerId: string
  shopName: string
  description: string | null
  productCount: number
}
