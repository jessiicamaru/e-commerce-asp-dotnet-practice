// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { SavedPage } from './types'

/**
 * Products the shopper saved for later (specs/075, #109). Every call is about the caller's own list: there is no
 * shopper id to send - the server reads it from the token.
 */
export class SavedProduct {
  /** Saving again is fine; a product not on sale is a 404, like its page. */
  static async save(productId: string): Promise<void> {
    await http.put(`/products/${productId}/saved`)
  }

  static async unsave(productId: string): Promise<void> {
    await http.delete(`/products/${productId}/saved`)
  }

  static async list(page: number, pageSize: number): Promise<SavedPage> {
    const { data } = await http.get<SavedPage>(`/products/saved?page=${page}&pageSize=${pageSize}`)
    return data
  }

  /** The ids alone - one request to draw every heart on a page of cards. */
  static async ids(): Promise<string[]> {
    const { data } = await http.get<string[]>('/products/saved/ids')
    return data
  }
}
