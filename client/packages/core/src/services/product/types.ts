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
  /** Who sells it; null means the shop itself (specs/027). */
  sellerId: string | null
  /** Their shop's name. Null when it is the shop itself - the storefront words that, not the server. */
  sellerName: string | null
  /** True when the shapes do not all cost the same, so the price is shown as "from" (specs/020). */
  priceVaries: boolean
  variantCount: number
  /** Filled on the product lookup, null on the listing. */
  variants: Variant[] | null
  /**
   * Where it stands with the moderators (specs/045). A shopper only ever sees Approved ones; its seller
   * and staff see the rest, with the reason for a rejection.
   */
  reviewStatus: ReviewStatus
  reviewReason: string | null
  /** The average of the visible reviews, null when there are none (specs/046). */
  ratingAverage: number | null
  ratingCount: number
  /**
   * On the lookup only (specs/124): the text as stored, for its seller to edit - `name` and `description` above fall
   * back per field, and once every language has its own text no read shows the original at all.
   */
  original?: ProductText | null
  translations?: ProductTranslationText[] | null
  /** On the lookup only (specs/159): the specifications of its category and department that have a value. */
  specifications?: ProductSpecification[] | null
  /**
   * On the lookup and the review queue (specs/160): the photographs after the cover (`imageUrl`), in order. Null on the
   * listing, which shows the cover alone.
   */
  photos?: ProductPhoto[] | null
  /**
   * What `price` is compared against - the compare-at of the variant giving the "from" price, in `currency` (specs/161).
   * Null when it is not reduced. Shown struck through, never charged.
   */
  compareAtPrice?: number | null
}

/** One of a product's photographs after its cover (specs/160). */
export interface ProductPhoto {
  id: string
  url: string
}

/** A product's own name and description - what was typed when it was listed (specs/124). */
export interface ProductText {
  name: string
  description: string | null
}

/** A product's own text in one language, as stored: a null description is none, not the original's (specs/124). */
export interface ProductTranslationText extends ProductText {
  language: string
}

/** What `PUT /api/products/{id}` sets (specs/124). */
export interface ProductDetails {
  name: string
  description: string | null
  categoryId: string
}

/** Another shape of a product (specs/020): its price is in the shop's DEFAULT currency, like listing one. */
export interface NewVariant {
  sku: string
  price: number
  options: { name: string; value: string }[]
}

export type ReviewStatus = 'Approved' | 'Pending' | 'Rejected'

/** One shape a product is sold in - what is priced, stocked and bought (specs/020). */
export interface Variant {
  id: string
  sku: string
  /** Null when this shape is not sold in the currency being browsed in (specs/022). */
  price: number | null
  currency: string
  /** What `price` is compared against, in the same currency, or null (specs/161). Display only. */
  compareAtPrice?: number | null
  /** The options in words: "Kit: Body only · Colour: Black". Empty for a product sold one way. */
  optionSummary: string
  /** `id` is what addresses an option, e.g. to translate it (specs/021). */
  options: { id: string; name: string; value: string }[]
  availability: 'InStock' | 'OutOfStock' | string
  isActive: boolean
  /**
   * This shape's own photograph, **or the product's when it has none** (specs/032).
   *
   * Already resolved by the server, so nothing here implements the fallback. A client doing it
   * itself and getting it wrong would show the previously chosen variant's picture — which looks
   * exactly like the feature working. Null only when neither has one.
   */
  imageUrl: string | null
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
  /** One shop's products (specs/099), for its page. */
  sellerId?: string
  /** A range of the "from" price, in the currency being browsed in - never converted (specs/109). */
  minPrice?: number
  maxPrice?: number
  /** Only what can be bought now. Sent only when true. */
  inStock?: boolean
  /** Only what is reduced - a compare-at in the currency being browsed in (specs/161). Sent only when true. */
  onSale?: boolean
  /** Products holding every one of these specification options (specs/159) - sent as one `optionIds` per id. */
  optionIds?: string[]
  /** Only these products (specs/163) - what this browser opened. At most 24. */
  ids?: string[]
}

/** One line of a product's specifications table, in the reader's language (specs/159). */
export interface ProductSpecification {
  specificationId: string
  name: string
  kind: 'Text' | 'Choice'
  optionId: string | null
  text: string | null
  /** What to show: the option translated, or the text as written. */
  value: string
}

/** What the seller's form sends: an option for a choice, a text for a text. */
export interface ProductSpecificationValue {
  specificationId: string
  optionId?: string
  text?: string
}

/**
 * What listing a product needs (specs/028).
 *
 * `price` is in the shop's **default** currency - see `Product.create`. There is no seller field
 * and there must never be one: the server takes the owner from the token.
 */
export interface NewProduct {
  name: string
  description: string | null
  price: number
  sku: string
  categoryId: string
}
