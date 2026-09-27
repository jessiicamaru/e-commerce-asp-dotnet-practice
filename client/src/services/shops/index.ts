// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Page } from '@/services/product/types'
import type { ShopFront, ShopState } from './types'

/**
 * Any shop's public page (specs/099) - its products come from the listing with a `sellerId` - and its state
 * (specs/107): the seller pauses and reopens their own, staff close and reopen any.
 */
export class Shops {
  static async get(sellerId: string): Promise<ShopFront> {
    const { data } = await http.get<ShopFront>(`/shops/${sellerId}`, { anonymous: true })
    return data
  }

  /** The caller's own shop - no seller id is sent; the server reads it from the token. */
  static async mine(): Promise<ShopState> {
    const { data } = await http.get<ShopState>('/shops/mine')
    return data
  }

  static async pause(): Promise<ShopState> {
    const { data } = await http.post<ShopState>('/shops/mine/pause')
    return data
  }

  static async resume(): Promise<ShopState> {
    const { data } = await http.post<ShopState>('/shops/mine/reopen')
    return data
  }

  static async closed(pageNumber: number, pageSize: number): Promise<Page<ShopState>> {
    const { data } = await http.get<Page<ShopState>>('/shops/closed', { params: { pageNumber, pageSize } })
    return data
  }

  static async close(sellerId: string, reason: string): Promise<ShopState> {
    const { data } = await http.post<ShopState>(`/shops/${sellerId}/close`, { reason })
    return data
  }

  static async reopen(sellerId: string): Promise<ShopState> {
    const { data } = await http.post<ShopState>(`/shops/${sellerId}/reopen`)
    return data
  }
}
