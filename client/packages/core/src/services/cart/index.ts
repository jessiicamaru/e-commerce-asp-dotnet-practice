// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@ecommerce/core/config/axios'
import type { GuestCartLine } from '@ecommerce/core/utils/cart/guest-cart'
import type { Cart as CartModel } from './types'

/**
 * The signed-in customer's cart. Cart stores product ids and quantities only; names and prices are
 * looked up from Catalog when it is read (specs/010).
 */
export class Cart {
  static async get(): Promise<CartModel> {
    const { data } = await http.get<CartModel>('/cart')
    return data
  }

  /** A variant is what is added: two shapes of one product are two lines (specs/020). */
  static async addItem(productId: string, quantity: number, variantId?: string): Promise<void> {
    await http.post('/cart/items', { productId, quantity, variantId })
  }

  static async setQuantity(productId: string, quantity: number): Promise<void> {
    await http.put(`/cart/items/${productId}`, { quantity })
  }

  static async removeItem(productId: string): Promise<void> {
    await http.delete(`/cart/items/${productId}`)
  }

  static async empty(): Promise<void> {
    await http.delete('/cart')
  }

  /**
   * Prices a signed-out shopper's lines exactly as a stored cart is priced, and stores nothing (specs/162). Anonymous: it
   * is asked before there is anybody to ask as.
   */
  static async price(lines: GuestCartLine[]): Promise<CartModel> {
    const { data } = await http.post<CartModel>('/cart/price', { lines }, { anonymous: true })
    return data
  }

  /** Merges the browser's lines into the signed-in shopper's cart - the larger quantity per shape, so a repeat is harmless. */
  static async merge(lines: GuestCartLine[]): Promise<void> {
    await http.post('/cart/merge', { lines })
  }
}
