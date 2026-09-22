import { http } from '@/config/axios'
import type { Shop } from './types'

/**
 * The signed-in seller's shop (specs/027).
 *
 * Every call is about the caller and sends no seller id: Identity reads who this is from the token.
 * An endpoint that took an id would let one seller rename another's shop.
 */
export class Seller {
  static async me(): Promise<Shop> {
    const { data } = await http.get<Shop>('/sellers/me')
    return data
  }

  static async rename(shopName: string): Promise<Shop> {
    const { data } = await http.put<Shop>('/sellers/me/shop-name', { shopName })
    return data
  }
}
