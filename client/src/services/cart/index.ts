// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
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

  static async addItem(productId: string, quantity: number): Promise<void> {
    await http.post('/cart/items', { productId, quantity })
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
}
