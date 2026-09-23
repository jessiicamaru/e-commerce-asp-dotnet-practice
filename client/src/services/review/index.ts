// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Page } from '@/services/product/types'
import type { MyReview, Review } from './types'

/**
 * Reviews (specs/046). Reading a product's is public. Writing is the caller's own and only after they
 * received it - the server decides that, from what Order told Catalog was delivered. Hiding is staff's.
 */
export class Reviews {
  static async forProduct(productId: string, page: number, pageSize: number): Promise<Page<Review>> {
    const { data } = await http.get<Page<Review>>(`/products/${productId}/reviews?pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async mine(productId: string): Promise<MyReview> {
    const { data } = await http.get<MyReview>(`/products/${productId}/reviews/mine`)
    return data
  }

  /** Writes the caller's review, or rewrites it: one per customer per product. */
  static async write(productId: string, rating: number, body: string): Promise<Review> {
    const { data } = await http.put<Review>(`/products/${productId}/reviews/mine`, { rating, body: body || null })
    return data
  }

  static async forStaff(hidden: boolean, page: number, pageSize: number): Promise<Page<Review>> {
    const { data } = await http.get<Page<Review>>(`/reviews?hidden=${hidden}&pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async hide(id: string, reason: string): Promise<Review> {
    const { data } = await http.post<Review>(`/reviews/${id}/hide`, { reason })
    return data
  }

  static async restore(id: string): Promise<Review> {
    const { data } = await http.post<Review>(`/reviews/${id}/restore`)
    return data
  }
}
