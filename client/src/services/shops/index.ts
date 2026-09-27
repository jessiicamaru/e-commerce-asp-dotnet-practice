// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { ShopFront } from './types'

/** Any shop's public page (specs/099) - its products come from the listing with a `sellerId`. */
export class Shops {
  static async get(sellerId: string): Promise<ShopFront> {
    const { data } = await http.get<ShopFront>(`/shops/${sellerId}`, { anonymous: true })
    return data
  }
}
