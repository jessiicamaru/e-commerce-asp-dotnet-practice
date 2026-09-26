// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { NewVoucher, VoucherPage, VoucherSummary } from './types'

/**
 * Vouchers (specs/069, 070): an administrator's are the platform's, a seller's their shop's. Nothing here names
 * an owner - the server reads it from the token, and refuses anybody who is neither.
 */
export class Voucher {
  static async create(voucher: NewVoucher): Promise<VoucherSummary> {
    const { data } = await http.post<VoucherSummary>('/vouchers', voucher)
    return data
  }

  /** The caller's own: the platform's for an administrator, theirs for a seller. Newest first. */
  static async mine(page: number, pageSize: number): Promise<VoucherPage> {
    const { data } = await http.get<VoucherPage>(`/vouchers/mine?page=${page}&pageSize=${pageSize}`)
    return data
  }

  /** From now on, not usable. Orders that used it keep it. */
  static async disable(id: string): Promise<VoucherSummary> {
    const { data } = await http.post<VoucherSummary>(`/vouchers/${id}/disable`)
    return data
  }
}
