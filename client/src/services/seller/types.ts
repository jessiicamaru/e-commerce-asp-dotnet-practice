/** The caller's own shop (specs/027). There is no type for anybody else's - no endpoint returns one. */
export interface Shop {
  sellerId: string
  shopName: string
  /** The seller's own words for their shop's page (specs/099); null when none. */
  description?: string | null
}
