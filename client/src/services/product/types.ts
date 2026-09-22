export interface Product {
  id: string
  name: string
  description: string | null
  /**
   * The cheapest shape's price, in `currency` - and **null when no shape of it is sold in that
   * currency** (specs/022). Null rather than 0, because 0 is a price and reads as a free camera.
   */
  price: number | null
  /** Which currency `price` is in, as the server echoed it back. */
  currency: string
  /** "InStock" / "OutOfStock" - a read model fed by Inventory, never a count (specs/004). */
  availability: 'InStock' | 'OutOfStock' | string
  sku: string
  categoryId: string
  isActive: boolean
  /** Null when there is none. Versioned, so it changes whenever the image does (specs/019). */
  imageUrl: string | null
  /** True when the shapes do not all cost the same, so the price is shown as "from" (specs/020). */
  priceVaries: boolean
  variantCount: number
  /** Filled on the product lookup, null on the listing. */
  variants: Variant[] | null
}

/** One shape a product is sold in - what is priced, stocked and bought (specs/020). */
export interface Variant {
  id: string
  sku: string
  /** Null when this shape is not sold in the currency being browsed in (specs/022). */
  price: number | null
  currency: string
  /** The options in words: "Kit: Body only · Colour: Black". Empty for a product sold one way. */
  optionSummary: string
  options: { name: string; value: string }[]
  availability: 'InStock' | 'OutOfStock' | string
  isActive: boolean
}

export interface Page<T> {
  items: T[]
  pageNumber: number
  totalPages: number
  totalCount: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export type SortBy = 'name_asc' | 'name_desc' | 'price_asc' | 'price_desc'

export interface ProductQuery {
  pageNumber?: number
  pageSize?: number
  categoryId?: string
  searchTerm?: string
  sortBy?: SortBy
}
