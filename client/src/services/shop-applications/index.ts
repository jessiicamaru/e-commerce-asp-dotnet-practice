// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { ShopApplication, ShopApplicationInput, ShopApplicationPage, ShopApplicationStatus } from './types'

/**
 * Asking to sell, and deciding (specs/044). Applying and reading one's own are the caller's - no id names
 * the applicant; the queue and the decisions are refused by the server for anybody but staff.
 */
export class ShopApplications {
  static async mine(): Promise<ShopApplication[]> {
    const { data } = await http.get<ShopApplication[]>('/shop-applications/mine')
    return data
  }

  static async apply(input: ShopApplicationInput): Promise<ShopApplication> {
    const { data } = await http.post<ShopApplication>('/shop-applications', {
      shopName: input.shopName,
      description: input.description || null,
      phone: input.phone || null,
    })
    return data
  }

  static async list(status: ShopApplicationStatus, page: number, pageSize: number): Promise<ShopApplicationPage> {
    const { data } = await http.get<ShopApplicationPage>(`/shop-applications?status=${status}&page=${page}&pageSize=${pageSize}`)
    return data
  }

  static async approve(id: string): Promise<ShopApplication> {
    const { data } = await http.post<ShopApplication>(`/shop-applications/${id}/approve`)
    return data
  }

  static async reject(id: string, reason: string): Promise<ShopApplication> {
    const { data } = await http.post<ShopApplication>(`/shop-applications/${id}/reject`, { reason })
    return data
  }
}
