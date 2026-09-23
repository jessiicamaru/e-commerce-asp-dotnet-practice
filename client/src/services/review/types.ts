/** A review as a shopper reads it (specs/046). The hidden fields and the product's name are for staff. */
export interface Review {
  id: string
  productId: string
  authorName: string
  rating: number
  body: string | null
  createdAt: string
  updatedAt: string
  edited: boolean
  productName: string | null
  hiddenAt: string | null
  hiddenReason: string | null
}

/** Whether the caller may review a product - they received it - and their review if they wrote one. */
export interface MyReview {
  eligible: boolean
  review: Review | null
}
