// Staff only. The model types come from the entities they are about: products and audit entries.
import { http } from '@/config/axios'
import type { AuditPage } from '@/services/audit/types'
import type { Page, Product, ReviewStatus } from '@/services/product/types'

/**
 * What moderators do to products (specs/045), and what they have decided. Every call is refused by the
 * server for anybody who is not Admin or Moderator.
 */
export class Moderation {
  /** The queue (Pending, oldest first) or the history of one status. */
  static async products(status: ReviewStatus, page: number, pageSize: number): Promise<Page<Product>> {
    const { data } = await http.get<Page<Product>>(`/products/review?status=${status}&pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async approve(productId: string): Promise<Product> {
    const { data } = await http.post<Product>(`/products/${productId}/approve`)
    return data
  }

  static async reject(productId: string, reason: string): Promise<Product> {
    const { data } = await http.post<Product>(`/products/${productId}/reject`, { reason })
    return data
  }

  /** An approved product, off the shelf. */
  static async takeDown(productId: string, reason: string): Promise<Product> {
    const { data } = await http.post<Product>(`/products/${productId}/take-down`, { reason })
    return data
  }

  /** The caller's own moderation decisions, newest first - the dashboard's "recently". */
  static async myDecisions(page: number, pageSize: number): Promise<AuditPage> {
    const { data } = await http.get<AuditPage>(`/audit/mine?page=${page}&pageSize=${pageSize}`)
    return data
  }
}
