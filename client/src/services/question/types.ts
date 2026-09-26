/**
 * A question about a product and its one answer (specs/076). A shopper reads no hidden answer and no reasons -
 * those, and the product's name, come only to whoever answers or moderates.
 */
export interface Question {
  id: string
  productId: string
  askerName: string
  body: string
  createdAt: string
  answer: string | null
  answeredAt: string | null
  answerEdited: boolean
  productName: string | null
  hiddenAt: string | null
  hiddenReason: string | null
  answerHiddenAt: string | null
  answerHiddenReason: string | null
}
