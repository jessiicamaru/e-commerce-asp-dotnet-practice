import { api } from './http'

// The signed-in customer's cart (#37). Cart stores product ids and quantities only; the name and price
// on each line are looked up from Catalog when the cart is read, so they are an estimate - the amount
// charged is decided at checkout (specs/010).

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
  estimatedTotal: number | null
  canCheckOut: boolean
  /** False when Catalog could not be reached: the lines are still there, without prices. */
  pricesAvailable: boolean
}

export const getCart = () => api<Cart>('/cart')

export const addToCart = (productId: string, quantity: number) =>
  api<void>('/cart/items', { method: 'POST', body: { productId, quantity } })

export const setQuantity = (productId: string, quantity: number) =>
  api<void>(`/cart/items/${productId}`, { method: 'PUT', body: { quantity } })

export const removeLine = (productId: string) => api<void>(`/cart/items/${productId}`, { method: 'DELETE' })

export const emptyCart = () => api<void>('/cart', { method: 'DELETE' })

/** Why a line cannot be bought, in words a shopper understands. Null when it can. */
export function lineProblem(status: string): string | null {
  switch (status) {
    case 'Available':
      return null
    case 'NotForSale':
      return 'No longer for sale.'
    case 'NoLongerAvailable':
      return 'This product has been removed from the shop.'
    case 'PriceUnavailable':
      return 'The price could not be checked right now.'
    default:
      return 'This item cannot be bought right now.'
  }
}
